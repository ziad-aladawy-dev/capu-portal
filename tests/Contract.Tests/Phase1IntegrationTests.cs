using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CapitalUniversity.API;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Audit;
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

public class Phase1IntegrationTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    // Seeded staff that intentionally LACK permissions.permissions.View /
    // permissions.roles.View, so they exercise the 403-forbidden path with a real
    // authenticated identity (post-SessionVersion middleware, random Guids no longer
    // authenticate at all and get 401, which is also valid behaviour — see
    // ProtectedEndpoint_WithoutToken_Returns401).
    private const string DeptHeadNid = "28102021234567";   // HOD-001
    private const string DeptHeadPwd = "admin123";
    private const string StudentNid = "30201011234567";    // 20250001
    private const string StudentPwd = "123456";

    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public Phase1IntegrationTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        var dbName = "TestDb_" + Guid.NewGuid();
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
    public async Task SecuredEndpoint_WithoutToken_Returns401_WithBearerChallenge()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/permissions");

        // Task 1 cleanup — was status-code-only. Now also pins that the
        // response carries a Bearer authentication challenge, so a mutation
        // that drops the JWT scheme from Program.cs surfaces here instead of
        // silently letting anonymous traffic through with a different 401.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        response.Headers.WwwAuthenticate.Should().Contain(h => h.Scheme == "Bearer");
    }

    public static IEnumerable<object[]> ForbiddenScenarios()
    {
        // (identifier, password, endpoint, label) — each pair authenticates
        // successfully but lacks the permission for the endpoint. Folded into
        // one Theory so the same body-shape assertions cover every case.
        yield return new object[] { DeptHeadNid, DeptHeadPwd, "/api/permissions", "dept-head/permissions" };
        yield return new object[] { DeptHeadNid, DeptHeadPwd, "/api/roles",       "dept-head/roles"       };
        yield return new object[] { StudentNid,  StudentPwd,  "/api/roles",       "student/roles"         };
        yield return new object[] { StudentNid,  StudentPwd,  "/api/permissions", "student/permissions"   };
    }

    [Theory]
    [MemberData(nameof(ForbiddenScenarios))]
    public async Task AuthenticatedCallerWithoutGrant_Returns403_WithNoBodyLeak(
        string identifier, string password, string endpoint, string label)
    {
        // Task 1 cleanup — four near-identical [Fact]s asserting "403 with no
        // grant" folded into one Theory. Each row also asserts no information
        // leak: the 403 body must NOT echo back the resource that was
        // requested (e.g. the offending path or a list of granted scopes),
        // which an over-eager error handler could accidentally leak.
        _ = label; // used for xUnit test-explorer readability only
        var client = await AuthenticatedClientAsync(identifier, password);

        var response = await client.GetAsync(endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "auth succeeded — only the permission gate refused");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(identifier,
            "the 403 response must not echo the caller identifier back to the wire");
    }

    [Fact]
    public async Task Request_NonExistentRoute_Returns404ProblemDetails()
    {
        var client = await AuthenticatedClientAsync(DeptHeadNid, DeptHeadPwd);
        var response404 = await client.GetAsync("/api/invalid");
        response404.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response404.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string nid, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = nid, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, $"login for {nid} must succeed against the seeded credentials");
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }
}
