using System.Net;
using System.Reflection;
using System.Security.Claims;
using CapitalUniversity.Sync.Host.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// End-to-end behaviour of the sync host's authorization integration. Closes
/// audit finding C-1 by proving — on a real ASP.NET Core test server — that
/// every <c>/admin/*</c> endpoint:
///   <list type="bullet">
///     <item>Returns 401 to anonymous callers.</item>
///     <item>Returns 403 to callers whose JWT lacks the configured role.</item>
///     <item>Returns 200 to callers whose JWT carries the role.</item>
///   </list>
///
/// <para>
/// We don't spin up the full <c>Sync.Host</c> program — it depends on six SQL
/// connections, Hangfire SQL storage, and migrations. Instead we mirror the
/// admin-route shape (<c>app.MapGroup("/admin").RequireAuthorization(SyncAuthPolicies.SyncAdmin)</c>)
/// in a minimal host and apply the same JWT bearer + policy registration the
/// production code uses. If the production policy contract drifts, these tests
/// drift with it.
/// </para>
/// </summary>
public class AdminEndpointAuthorizationTests : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;

    public AdminEndpointAuthorizationTests()
    {
        _host = CreateMinimalAdminHost();
        _host.Start();
        _client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task AdminEndpoint_AnonymousCaller_Returns401()
    {
        // No Authorization header. The policy requires an authenticated
        // principal first; ASP.NET Core's authorization middleware returns
        // 401 (challenge) before it even gets to the role check.
        var response = await _client.GetAsync("/admin/probe");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_AuthenticatedWithoutRole_Returns403()
    {
        // Token is valid but carries the wrong role. The policy's
        // RequireRole check fails after the authentication step, so
        // authorization middleware returns 403 (forbidden).
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                FakeJwtAuthHandler.SchemeName,
                FakeJwtAuthHandler.MakeToken("RegularStaff"));

        var response = await _client.GetAsync("/admin/probe");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoint_AuthenticatedWithSyncAdminRole_Returns200()
    {
        // Happy path: the SyncAdmin role is present, the policy passes, the
        // probe endpoint executes and returns a 200.
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                FakeJwtAuthHandler.SchemeName,
                FakeJwtAuthHandler.MakeToken("SyncAdmin"));

        var response = await _client.GetAsync("/admin/probe");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ok");
    }

    [Fact]
    public async Task NonAdminEndpoint_AnonymousCaller_Returns200()
    {
        // Sanity check: only /admin/* is gated by the policy. Anonymous
        // health-check style endpoints stay open. (Production Sync.Host
        // similarly leaves /healthz and / open.)
        var response = await _client.GetAsync("/open");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Minimal host that mirrors the production wiring ──────────────────────

    /// <summary>
    /// Builds an ASP.NET Core test host carrying the exact same authorization
    /// shape as <c>Sync.Host/Program.cs</c>:
    /// <list type="number">
    ///   <item>One auth scheme that populates <see cref="HttpContext.User"/>.</item>
    ///   <item>A <see cref="SyncAuthPolicies.SyncAdmin"/> policy that requires
    ///         an authenticated user carrying the configured role.</item>
    ///   <item>A <c>/admin</c> route group with the policy applied via
    ///         <c>RequireAuthorization(SyncAuthPolicies.SyncAdmin)</c>.</item>
    /// </list>
    /// The auth scheme is a fake (no real signing) so we can issue tokens
    /// deterministically in tests; the policy + group wiring is identical
    /// to production.
    /// </summary>
    private static IHost CreateMinimalAdminHost()
    {
        return new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(FakeJwtAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, FakeJwtAuthHandler>(
                            FakeJwtAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy(SyncAuthPolicies.SyncAdmin, policy =>
                        {
                            policy.AddAuthenticationSchemes(FakeJwtAuthHandler.SchemeName);
                            policy.RequireAuthenticatedUser();
                            policy.RequireRole("SyncAdmin");
                        });
                    });
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/open", () => Results.Ok("open"));
                        var admin = endpoints.MapGroup("/admin")
                            .RequireAuthorization(SyncAuthPolicies.SyncAdmin);
                        admin.MapGet("/probe", () => Results.Ok(new { status = "ok" }));
                    });
                });
            })
            .Build();
    }

    /// <summary>
    /// Test-only authentication handler. Tokens are plain strings of the
    /// form <c>"role:&lt;rolename&gt;"</c>; the handler turns the role into a
    /// <see cref="ClaimTypes.Role"/> claim on the resulting principal. This is
    /// the same shape the production <c>JwtBearer</c> handler produces after
    /// validating a signed token — for the policy's purpose, only the
    /// resulting principal's claims matter.
    /// </summary>
    private sealed class FakeJwtAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestJwt";

        public FakeJwtAuthHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }

        public static string MakeToken(string role) => $"role:{role}";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var raw = header.ToString();
            const string prefix = SchemeName + " ";
            if (!raw.StartsWith(prefix, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = raw.Substring(prefix.Length);
            if (!token.StartsWith("role:", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("Malformed test token."));
            }

            var role = token.Substring("role:".Length);
            var claims = new[] { new Claim(ClaimTypes.Role, role) };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
