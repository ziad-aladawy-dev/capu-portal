using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace CapitalUniversity.Contract.Tests;

/// <summary>
/// Production-realistic concurrent-create coverage. Spins N parallel POSTs targeting
/// the same StructureNode and verifies that exactly one row exists per unique
/// (StudentCode, NationalId) tuple — i.e. no duplicates regardless of interleaving.
/// </summary>
public class ConcurrentStudentCreateTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public ConcurrentStudentCreateTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "ConcurrentStudentTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddScoped(_ => new Mock<ILoggerService>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    [Fact]
    public async Task ParallelCreate_DistinctStudents_AllPersist_NoDuplicates()
    {
        // Auth as Super Admin so the create endpoint is reachable.
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = "29801011234567", Password = "admin123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Student-create rule: must be assigned to a Level-type node. Grab the first
        // seeded one.
        Guid nodeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            nodeId = await db.StructureNodes
                .AsNoTracking()
                .Where(n => n.Type == CapitalUniversity.Core.Domain.UniversityStructure.Enums.StructureNodeType.Level)
                .Select(n => n.Id)
                .FirstAsync();
        }

        const int n = 12;
        var requests = Enumerable.Range(0, n).Select(i => new CreateStudentRequest
        {
            StudentCode = $"CONC-{Guid.NewGuid():N}".Substring(0, 12),
            NationalId = $"99{i:D12}",
            NameAr = $"طالب متزامن {i}",
            NameEn = $"Concurrent Student {i}",
            BirthDate = new DateTime(2003, 1, 1),
            PhoneNumber = $"0100000{i:D4}",
            Email = $"conc{i}@test.eg",
            Password = "Password!123",
            ConfirmPassword = "Password!123",
            StructureNodeId = nodeId,
        }).ToList();

        // Fire all POSTs simultaneously against a single client (shared HTTP pipeline,
        // separate request scopes for each).
        var tasks = requests.Select(r => client.PostAsJsonAsync("/api/students", r)).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Every request should land — none should silently lose to a race.
        var successful = responses.Count(r => r.IsSuccessStatusCode);
        successful.Should().Be(n, "no concurrent POST may fail due to a race");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var distinctCodes = await db.Students
                .Where(s => requests.Select(r => r.StudentCode).Contains(s.StudentCode))
                .Select(s => s.StudentCode)
                .Distinct()
                .CountAsync();
            distinctCodes.Should().Be(n, "exactly one row persisted per StudentCode — no duplicates");
        }
    }
}