using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
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
/// Student-orphan fix: a Student principal holds no role grants, so before the
/// implicit StudentSelfPermissions set the student-facing endpoints 403'd and
/// students could authenticate but never use the portal. These tests prove a
/// seeded student (a) reaches their own academic endpoints, and (b) is still
/// denied an ops/admin endpoint — i.e. the implicit grant is narrow.
/// </summary>
public class StudentSelfAccessApiTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    // Seeded student (DataSeeder.SeedStudentsAsync): NationalId login, pwd "123456".
    private const string StudentNid = "30201011234567";
    private const string StudentPwd = "123456";

    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public StudentSelfAccessApiTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "StudentSelfAccessDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoreDbContext>>();
                services.RemoveAll<CoreDbContext>();
                services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    private async Task<HttpClient> LoginAsStudentAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = StudentNid, Password = StudentPwd });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the seeder must populate the student row with the documented credentials");
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    [Fact]
    public async Task Login_ReturnsRole_SoTheSpaCanGateStudentRoutes()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = StudentNid, Password = StudentPwd });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        // The SPA reads user.role to recognise a context-scoped student (whose
        // permission list is empty) and admit them to the student surface.
        body!.User.Role.Should().Be("Student");
    }

    [Theory]
    [InlineData("/api/grades/history")]
    [InlineData("/api/grades/summary")]
    [InlineData("/api/transcript")]
    [InlineData("/api/courses/registered")]
    public async Task Student_CanReach_OwnAcademicEndpoints(string path)
    {
        var client = await LoginAsStudentAsync();

        var response = await client.GetAsync(path);

        // The fix: a student is no longer Forbidden on their own self-service
        // endpoints. (Content may be empty when nothing is synced — still 200.)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_IsStillDenied_AdminEndpoint()
    {
        var client = await LoginAsStudentAsync();

        // /api/permissions requires an ops permission a student never holds — the
        // implicit self-grant must NOT leak into admin surface.
        var response = await client.GetAsync("/api/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_CanReadOwnFees_ButNotAnotherStudents()
    {
        // Resolve the logged-in student's own id from the seeded store.
        Guid ownId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            ownId = await db.Students.Where(s => s.NationalId == StudentNid).Select(s => s.Id).FirstAsync();
        }

        var client = await LoginAsStudentAsync();

        // B8 — own fees: payments.orders.View is held implicitly + self-scope passes.
        var own = await client.GetAsync($"/api/payments/fees/by-student/{ownId}");
        own.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);

        // Another student's fees: self-scope (PermissionScopeKind.Student) denies it.
        var other = await client.GetAsync($"/api/payments/fees/by-student/{Guid.NewGuid()}");
        other.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
