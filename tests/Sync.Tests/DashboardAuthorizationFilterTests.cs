using System.Security.Claims;
using CapitalUniversity.Sync.Host.Configuration;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Behavioural pins for the Hangfire dashboard's role-based gate. The dashboard
/// is mounted at <c>/hangfire</c> by <c>Sync.Host/Program.cs</c> and is the
/// single most sensitive surface in the sync host — it exposes job internals,
/// retry queues, and admin actions. These tests close audit finding C-1
/// (admin endpoints had no real auth) by proving that the filter:
///   <list type="bullet">
///     <item>Rejects anonymous callers (no <c>HttpContext.User.Identity</c> or
///       unauthenticated identity) regardless of which roles it was constructed
///       with.</item>
///     <item>Rejects authenticated callers whose token carries no matching
///       role claim.</item>
///     <item>Accepts authenticated callers carrying at least one of the
///       accepted roles.</item>
///     <item>The <c>AuthenticatedOnly</c> factory passes any authenticated
///       caller without inspecting roles — the documented fallback when role
///       claims aren't issued.</item>
///     <item>Construction with no accepted roles is a constructor error so
///       deployers can't silently produce a permissive filter.</item>
///   </list>
/// </summary>
public class DashboardAuthorizationFilterTests
{
    /// <summary>
    /// Build a DashboardContext that forwards the supplied principal to the
    /// filter through Hangfire's <c>GetHttpContext()</c> extension. The
    /// production filter only reads <c>HttpContext.User</c>; storage/options
    /// are minimal stubs the base class requires for construction.
    /// </summary>
    private static DashboardContext BuildContext(ClaimsPrincipal user)
    {
        // AspNetCoreDashboardContext touches HttpContext.RequestServices
        // during construction; an empty provider satisfies it without
        // pulling in any of the real Hangfire infrastructure.
        var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { User = user, RequestServices = services };
        return new AspNetCoreDashboardContext(
            new Mock<Hangfire.JobStorage>(MockBehavior.Loose).Object,
            new DashboardOptions(),
            httpContext);
    }

    private static ClaimsPrincipal Anonymous() =>
        new(new ClaimsIdentity()); // unauthenticated — no auth-type set

    private static ClaimsPrincipal AuthenticatedWithRoles(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestJwt"));
    }

    [Fact]
    public void AnonymousCaller_IsRejected_EvenWhenRolesMatch()
    {
        // A ClaimsIdentity with no authenticationType reports
        // Identity.IsAuthenticated == false. The role match never executes.
        var filter = new RoleBasedDashboardAuthorizationFilter("SyncAdmin");
        var context = BuildContext(Anonymous());

        filter.Authorize(context).Should().BeFalse(
            "the role-based filter must short-circuit on unauthenticated users — " +
            "a role claim on an unverified principal proves nothing");
    }

    [Fact]
    public void AuthenticatedCallerWithoutRole_IsRejected()
    {
        var filter = new RoleBasedDashboardAuthorizationFilter("SyncAdmin");
        var context = BuildContext(AuthenticatedWithRoles("RegularStaff"));

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void AuthenticatedCallerWithMatchingRole_IsAccepted()
    {
        var filter = new RoleBasedDashboardAuthorizationFilter("SyncAdmin");
        var context = BuildContext(AuthenticatedWithRoles("SyncAdmin"));

        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void AuthenticatedCaller_MatchingOneOfMultipleAcceptedRoles_IsAccepted()
    {
        // Operator usage: pass multiple accepted roles for OR-semantics
        // (any matching role passes). A caller in only one of them is in.
        var filter = new RoleBasedDashboardAuthorizationFilter("SyncAdmin", "PlatformAdmin");
        var context = BuildContext(AuthenticatedWithRoles("PlatformAdmin"));

        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void Constructor_RejectsEmptyRoleList()
    {
        // The "no roles" case is a footgun: a future contributor passing
        // Array.Empty<string>() would silently produce a deny-everyone
        // filter. The constructor throws so the misconfig surfaces at
        // composition time, not at first dashboard request.
        Action act = () => new RoleBasedDashboardAuthorizationFilter();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuthenticatedOnly_AcceptsAuthenticatedRegardlessOfRoles()
    {
        // The documented fallback for deployments where role claims aren't
        // issued. The factory bypasses role inspection; any authenticated
        // principal passes.
        var filter = RoleBasedDashboardAuthorizationFilter.AuthenticatedOnly();
        var context = BuildContext(AuthenticatedWithRoles("CompletelyUnrelatedRole"));

        filter.Authorize(context).Should().BeTrue();
    }

    [Fact]
    public void AuthenticatedOnly_StillRejectsAnonymous()
    {
        // Even the "any authenticated user passes" path must reject
        // unauthenticated callers — that's the whole point of the gate.
        var filter = RoleBasedDashboardAuthorizationFilter.AuthenticatedOnly();
        var context = BuildContext(Anonymous());

        filter.Authorize(context).Should().BeFalse();
    }

    [Fact]
    public void SyncAuthOptions_DoesNotCarryEnvironmentCoupledBypass()
    {
        // P0-2 contract pin: the prior versions of this options class exposed
        // a `DevAllowAnonymous` flag that, when combined with
        // IsDevelopment(), opened the dashboard and admin endpoints to
        // anonymous callers. A misconfigured ASPNETCORE_ENVIRONMENT on a
        // deployed image was then the single signal between fully gated and
        // open. The audit ruled the pattern unacceptable and this test fails
        // CI if anyone re-introduces an environment-coupled bypass flag.
        var properties = typeof(CapitalUniversity.Sync.Infrastructure.Configuration.SyncAuthOptions)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        properties.Should().NotContain("DevAllowAnonymous",
            "P0-2: dev-anonymous flag was retired; SyncAuthOptions must not carry any " +
            "environment-coupled bypass property");
        properties.Should().NotContain("AllowAnonymous",
            "any AllowAnonymous-shaped property would reintroduce the audit-flagged risk");
    }

    [Fact]
    public void AllowAllDashboardAuthorizationFilter_TypeIsRetired_NotResolvable()
    {
        // The retired `AllowAllDashboardAuthorizationFilter` always returned
        // true and was the production bypass that audit P0-2 closed. Pinning
        // its non-existence here prevents a future contributor from quietly
        // re-adding it as a "convenience" in dev.
        var sourceAssembly = typeof(RoleBasedDashboardAuthorizationFilter).Assembly;
        var retired = sourceAssembly.GetType(
            "CapitalUniversity.Sync.Host.Configuration.AllowAllDashboardAuthorizationFilter");

        retired.Should().BeNull(
            "the unconditional-allow filter was retired in audit P0-2 and must not be reintroduced");
    }
}
