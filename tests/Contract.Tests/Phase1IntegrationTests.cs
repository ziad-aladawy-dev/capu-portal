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
                services.RemoveAll(typeof(DbContextOptions<CoreDbContext>));
                services.RemoveAll(typeof(CoreDbContext));
                services.AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddScoped(_ => new Mock<IAppLogger>().Object);
                services.AddScoped(_ => new Mock<ILoggerService>().Object);
                services.AddSingleton(_ => new Mock<IMongoClient>().Object);
            });
        });
    }

    [Fact]
    public async Task SecuredEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PermissionsEndpoint_AuthenticatedButNoGrant_Returns403()
    {
        // Department Head is a real seeded staff: token validates, SessionVersion
        // matches, but they don't hold permissions.permissions.View → 403.
        var client = await AuthenticatedClientAsync(DeptHeadNid, DeptHeadPwd);
        var response = await client.GetAsync("/api/permissions");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RolesEndpoint_AuthenticatedButNoGrant_Returns403()
    {
        var client = await AuthenticatedClientAsync(DeptHeadNid, DeptHeadPwd);
        var response = await client.GetAsync("/api/roles");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RolesEndpoint_Student_Returns403()
    {
        var client = await AuthenticatedClientAsync(StudentNid, StudentPwd);
        var response = await client.GetAsync("/api/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PermissionsEndpoint_Student_Returns403()
    {
        var client = await AuthenticatedClientAsync(StudentNid, StudentPwd);
        var response = await client.GetAsync("/api/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
