using Hangfire.Dashboard;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Phase X.5 production auth scaffold. Replaces
/// <see cref="AllowAllDashboardAuthorizationFilter"/> for deployment scenarios where
/// the Hangfire dashboard + admin endpoints must enforce role-based access.
///
/// <para>
/// <b>Operator swap-in pattern</b> (in <c>Program.cs</c>, replacing the existing
/// AllowAll registration):
/// <code>
/// app.UseHangfireDashboard("/hangfire", new DashboardOptions
/// {
///     Authorization = new[] { new RoleBasedDashboardAuthorizationFilter("SyncAdmin") },
///     DisplayStorageConnectionString = false,
///     DashboardTitle = "CapU Sync — Hangfire"
/// });
/// </code>
/// </para>
///
/// <para>
/// <b>Auth pipeline requirements</b>:
/// <list type="number">
///   <item>An auth scheme that populates <c>HttpContext.User</c> (cookie, JWT bearer,
///   Windows, etc.) must be configured BEFORE the dashboard middleware.</item>
///   <item>The role/claim values are operator-supplied via the constructor — the
///   sync platform doesn't decide what role gates the dashboard.</item>
///   <item>For multi-role policies, pass multiple role names; the filter
///   <c>OR</c>s them (any matching role passes).</item>
/// </list>
/// </para>
///
/// <para>
/// This class is a <b>scaffold</b>, not a complete production solution. Real
/// deployments will additionally want:
/// </para>
/// <list type="bullet">
///   <item>Anti-CSRF on admin POST endpoints (`/admin/trigger/...`, `/admin/reaper/run`,
///   `/admin/retention/run`, `/admin/replay/...`).</item>
///   <item>An audit log of dashboard access (the dashboard exposes Hangfire job
///   internals; access SHOULD be recorded).</item>
///   <item>Stricter role separation between read-only dashboard viewers and admin-
///   action callers.</item>
/// </list>
/// </summary>
public sealed class RoleBasedDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string[] _acceptedRoles;
    private readonly bool _requireAuthenticated;

    public RoleBasedDashboardAuthorizationFilter(params string[] acceptedRoles)
    {
        if (acceptedRoles is null || acceptedRoles.Length == 0)
        {
            throw new ArgumentException(
                "RoleBasedDashboardAuthorizationFilter requires at least one accepted role. " +
                "If you want any authenticated user to pass, use AuthenticatedOnly() instead.",
                nameof(acceptedRoles));
        }
        _acceptedRoles = acceptedRoles;
        _requireAuthenticated = true;
    }

    private RoleBasedDashboardAuthorizationFilter(bool requireAuthenticatedOnly)
    {
        _acceptedRoles = Array.Empty<string>();
        _requireAuthenticated = requireAuthenticatedOnly;
    }

    /// <summary>
    /// Factory for the "any authenticated user passes" policy — useful when role
    /// claims aren't available but the auth pipeline still enforces login.
    /// </summary>
    public static RoleBasedDashboardAuthorizationFilter AuthenticatedOnly() =>
        new(requireAuthenticatedOnly: true);

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return false;
        }

        if (_acceptedRoles.Length == 0)
        {
            return _requireAuthenticated;
        }

        foreach (var role in _acceptedRoles)
        {
            if (user.IsInRole(role))
            {
                return true;
            }
        }

        return false;
    }
}