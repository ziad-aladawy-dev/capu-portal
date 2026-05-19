using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;
using System.Diagnostics;
using MongoDB.Driver;

namespace CapitalUniversity.Contract.Tests;

public class CachedAuthorizationTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;
    private readonly string _dbName;

    public CachedAuthorizationTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        // Hoist the in-memory DB name so the seeder + test queries land in the same store.
        _dbName = "CachedAuthTestDb_" + Guid.NewGuid();
        var capturedName = _dbName;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(options => options.UseInMemoryDatabase(capturedName));

                // Mock MongoDB and Logging
                services.RemoveAll<IMongoClient>();
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
                services.RemoveAll<IMongoDatabase>();
                services.AddScoped(_ => new Mock<IMongoDatabase>().Object);
                
                services.RemoveAll<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>();
                services.AddScoped(_ => new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>().Object);
                
                services.RemoveAll<CapitalUniversity.Core.Abstractions.CrossCutting.Audit.ILoggerService>();
                services.AddScoped(_ => new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Audit.ILoggerService>().Object);
            });
        });
    }

    [Fact]
    public async Task FullUseCase_Login_CachePopulated_ScopedAuthorization_PerformanceCheck()
    {
        // 1. Arrange - Seed Data
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            
            // Seed Staff with a real StructureNode FK (entity requires it).
            var anyNodeId = await db.StructureNodes.AsNoTracking().Select(n => n.Id).FirstAsync();
            var staff = new Staff
            {
                Id = Guid.NewGuid(),
                Name = "Auth User",
                EmployeeCode = "AUTH-001",
                NationalId = "12345",
                Role = "Viewer",
                StructureNodeId = anyNodeId,
                PasswordHash = hasher.HashPassword("password"),
                IsActive = true,
            };
            db.Staffs.Add(staff);

            // Seed Module/Service in the canonical form the manifest synchroniser owns.
            // Resource "academic-years" → "academics.academic-years.View", which is
            // what AcademicYearsController.GetAll binds against post-migration.
            var module = new Module { Id = Guid.NewGuid(), DisplayName = "Academic Timeline", ModuleKey = "academics" };
            var service = new Service { Id = Guid.NewGuid(), Module = module, ModuleId = module.Id, DisplayName = "View Academic Years" };
            db.Modules.Add(module);
            db.Services.Add(service);

            // Role with only View permission on the academic-years resource.
            var role = new Role { Id = Guid.NewGuid(), Name = "Viewer" };
            db.Roles.Add(role);
            db.RolePermissions.Add(new RolePermission(role.Id, service.Id, "academic-years", ActionLevel.View));

            // Assign role to staff with truly-global structural scope so the request
            // (which sends no X-StructureNode-Id) doesn't get filtered out.
            db.StaffRoles.Add(new StaffRoleAssignment(staff.Id, role.Id, "Global", "Global"));

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        // 2. Act - Login
        var loginRequest = new LoginRequestDto { Identifier = "12345", Password = "password" };
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.Token);

        // 3. Action Inside Scope (View) - This should trigger cache population via PermissionHandler
        var viewResponse = await client.GetAsync("/api/academic-years");
        viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Verify Cache - Should have permissions now.
        // Cache key format (PR-B3): perm_lookup_{epoch}_{userId}_{versionStamp}_{year}_{semester}_{structureNode}.
        // Both the epoch and per-user version stamp are random Guids lazily seeded
        // on first miss; read them back the same way the service does so the
        // assertion isn't tied to the values.
        var cache = _factory.Services.GetRequiredService<ICacheService>();
        var epoch = await cache.GetAsync<string>("perm_lookup_epoch");
        var versionStamp = await cache.GetAsync<string>($"perm_lookup_v_{authResult.User.Id}");
        epoch.Should().NotBeNullOrEmpty("global cache epoch must be seeded on first lookup");
        versionStamp.Should().NotBeNullOrEmpty("the lookup must have populated the version stamp on first miss");

        var cacheKey = $"perm_lookup_{epoch}_{authResult.User.Id}_{versionStamp}_Global_Global_Global";
        var cachedPerms = await cache.GetAsync<HashSet<string>>(cacheKey);

        cachedPerms.Should().NotBeNull();
        // PermissionIdentity.Create PascalCases module/resource segments (split on
        // dashes), so the lookup-time canonical form for the lowercase manifest names
        // is "Academics.AcademicYears.*". The string constants in PermissionNames stay
        // lowercase; HasPermissionAttribute normalises them via Parse + Create.
        cachedPerms!.Should().Contain("Academics.AcademicYears.View");
        cachedPerms.Should().NotContain("Academics.AcademicYears.Insert");

        // 5. Action Outside Scope (Insert)
        var createRequest = new { Name = "New Year", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddYears(1) };
        var createResponse = await client.PostAsJsonAsync("/api/academic-years", createRequest);
        
        // Should be 403 Forbidden because the user only has View level
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. Performance Check
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await client.GetAsync("/api/academic-years");
        }
        sw.Stop();
        
        System.Console.WriteLine($"100 authorized requests took: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(2000); 
    }
}
