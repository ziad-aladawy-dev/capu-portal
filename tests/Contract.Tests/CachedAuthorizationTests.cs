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
        _dbName = "CachedAuthTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<CoreDbContext>));
                services.RemoveAll(typeof(CoreDbContext));
                services.AddDbContext<CoreDbContext>(options => options.UseInMemoryDatabase(_dbName));

                // Mock MongoDB and Logging
                services.RemoveAll(typeof(IMongoClient));
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
                services.RemoveAll(typeof(IMongoDatabase));
                services.AddScoped(_ => new Mock<IMongoDatabase>().Object);
                
                services.RemoveAll(typeof(CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger));
                services.AddScoped(_ => new Mock<CapitalUniversity.Core.Abstractions.CrossCutting.Logging.IAppLogger>().Object);
                
                services.RemoveAll(typeof(CapitalUniversity.Core.Abstractions.CrossCutting.Audit.ILoggerService));
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
            
            // Seed a Staff user
            var staff = new Staff 
            { 
                Id = Guid.NewGuid(), 
                Name = "Auth User", 
                NationalId = "12345", 
                PasswordHash = hasher.HashPassword("password") 
            }; 
            db.Staffs.Add(staff);

            // Seed Module/Service
            var module = new Module { Id = Guid.NewGuid(), DisplayName = "Academic", ModuleKey = "Academic" };
            var service = new Service { Id = Guid.NewGuid(), Module = module, DisplayName = "Year" };
            db.Modules.Add(module);
            db.Services.Add(service);

            // Seed Role with only "View" permission
            var role = new Role { Id = Guid.NewGuid(), Name = "Viewer" };
            db.Roles.Add(role);
            db.RolePermissions.Add(new RolePermission(role.Id, service.Id, "Year", ActionLevel.View));

            // Assign role to staff (Global scope for simplicity)
            db.StaffRoles.Add(new StaffRoleAssignment(staff.Id, role.Id, "Global", "Global") 
            { 
                StructureNodePath = null 
            });

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

        // 4. Verify Cache - Should have permissions now
        var cache = _factory.Services.GetRequiredService<ICacheService>();
        var cacheKey = $"perm_lookup_{authResult.User.Id}_Global_Global";
        var cachedPerms = await cache.GetAsync<HashSet<string>>(cacheKey);
        
        cachedPerms.Should().NotBeNull();
        cachedPerms.Should().Contain("Academic.Year.View");
        cachedPerms.Should().NotContain("Academic.Year.Insert");

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
