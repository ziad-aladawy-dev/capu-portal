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

/// <summary>
/// Real HTTP-pipeline tests for the SessionVersion stateless-revocation flow:
/// login → access, missing-token → 401, logout → invalidates, password change →
/// invalidates, parallel requests safe under a single valid token.
/// </summary>
public class SessionVersionTests : IClassFixture<WebApplicationFactory<ModulesRegistry>>
{
    private const string SuperAdminNid = "29801011234567";
    private const string SuperAdminPwd = "admin123";

    private readonly WebApplicationFactory<ModulesRegistry> _factory;

    public SessionVersionTests(WebApplicationFactory<ModulesRegistry> factory)
    {
        // Hoist the DB name to a closure-captured variable. If the Guid lives inside
        // the options lambda, EF generates a fresh name per DbContext instantiation,
        // which means the seeder and the test query land in different in-memory stores.
        var dbName = "SessionVersionTestDb_" + Guid.NewGuid();
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

    private static async Task<string> LoginAsSuperAdminAsync(HttpClient client)
    {
        var (access, _) = await LoginAsSuperAdminPairAsync(client);
        return access;
    }

    private static async Task<(string Access, string Refresh)> LoginAsSuperAdminPairAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = SuperAdminNid, Password = SuperAdminPwd });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the seeder must have populated the Super Admin row with the documented credentials");

        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace(
            "login now issues a refresh token alongside the access token");
        return (body.Token, body.RefreshToken);
    }

    [Fact]
    public async Task SeederSanityCheck()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var count = await db.Staffs.CountAsync();
        var khaledExists = await db.Staffs.AnyAsync(s => s.NationalId == SuperAdminNid);
        count.Should().BeGreaterThan(0, "seeder must populate Staffs in Testing env");
        khaledExists.Should().BeTrue("Super Admin row required for login tests");
    }

    [Fact]
    public async Task Login_ReturnsToken_ThatGrantsAccessToProtectedEndpoint()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsSuperAdminAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Super Admin holds permissions.permissions.View");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_InvalidatesTheTokenItWasCalledWith()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsSuperAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Sanity: token is currently good.
        (await client.GetAsync("/api/permissions")).StatusCode.Should().Be(HttpStatusCode.OK);

        var logout = await client.PostAsync("/api/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Same token, replayed → middleware sees stale session_version → 401.
        var after = await client.GetAsync("/api/permissions");
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_InvalidatesEveryOutstandingToken()
    {
        var client = _factory.CreateClient();
        var tokenA = await LoginAsSuperAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var changed = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequestDto { CurrentPassword = SuperAdminPwd, NewPassword = "new-password-1234" });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The token used to make the call itself becomes invalid because the bump
        // also incremented the version it carried.
        var afterChange = await client.GetAsync("/api/permissions");
        afterChange.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Restore creds for any later test runs sharing the seeded DB instance — this
        // factory uses a per-test in-memory DB, so this isn't strictly required, but
        // makes intent explicit.
        var freshClient = _factory.CreateClient();
        var reLogin = await freshClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Identifier = SuperAdminNid, Password = "new-password-1234" });
        reLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ParallelRequests_WithSameValidToken_AllSucceed()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsSuperAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 16 concurrent calls. None should be rate-limited or fail; the middleware
        // is a simple version equality check and must be thread-safe.
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => client.GetAsync("/api/permissions"))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK,
            "stateless validation must not race or invalidate a still-valid token under concurrency");
    }

    [Fact]
    public async Task Refresh_IssuesNewToken_ThatStillWorks()
    {
        var client = _factory.CreateClient();
        var (oldToken, refreshToken) = await LoginAsSuperAdminPairAsync(client);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await refresh.Content.ReadFromJsonAsync<RefreshTokenResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Token.Should().NotBe(oldToken,
            "refresh must mint a new jti-distinct token even when the version claim is unchanged");
        body.RefreshToken.Should().NotBeNullOrWhiteSpace().And.NotBe(refreshToken,
            "rotation must return a fresh refresh token, invalidating the presented one");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        var afterRefresh = await client.GetAsync("/api/permissions");
        afterRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithRotatedToken_ReplayDetected_RevokesEverything()
    {
        var client = _factory.CreateClient();
        var (access, refreshToken) = await LoginAsSuperAdminPairAsync(client);

        // First refresh — succeeds and yields a successor refresh token.
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = refreshToken });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstRefresh.Content.ReadFromJsonAsync<RefreshTokenResponseDto>();

        // Replay the original (now-revoked, but-rotated) token. Must fail AND the
        // service must bump SessionVersion, taking the still-issued successor down too.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = refreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "presenting a rotated refresh token is treated as a compromise signal");

        // The successor access token also stops working — replay triggered a session bump.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody!.Token);
        var afterReplay = await client.GetAsync("/api/permissions");
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "session-version bump on replay must invalidate every still-live access token");
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_AfterLogout_Returns401()
    {
        var client = _factory.CreateClient();
        var (access, refreshToken) = await LoginAsSuperAdminPairAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var logout = await client.PostAsync("/api/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "logout must revoke every refresh token, so the previously issued one is dead");
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var client = _factory.CreateClient();
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = "not-a-real-token-value" });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}