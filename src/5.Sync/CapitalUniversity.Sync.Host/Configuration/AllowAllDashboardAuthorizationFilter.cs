using Hangfire.Dashboard;

namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Development-only dashboard filter. Production must replace this with proper auth.
/// </summary>
internal sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}