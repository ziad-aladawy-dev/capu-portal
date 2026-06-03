namespace CapitalUniversity.Sync.Host.Configuration;

/// <summary>
/// Named authorization policies used by the sync host. Kept as a small
/// constants class so the policy name is referenced symbolically from both
/// the registration site (in <c>Program.cs</c>) and the endpoint gates
/// (<c>RequireAuthorization(SyncAuthPolicies.SyncAdmin)</c>).
/// </summary>
public static class SyncAuthPolicies
{
    /// <summary>
    /// Gates every <c>/admin/*</c> endpoint and the Hangfire dashboard.
    /// Requires an authenticated principal carrying the role declared in
    /// <see cref="CapitalUniversity.Sync.Infrastructure.Configuration.SyncAuthOptions.RequiredRole"/>.
    /// In dev, the policy degrades to "any caller passes" only when
    /// <c>Sync:Auth:DevAllowAnonymous</c> is explicitly set true.
    /// </summary>
    public const string SyncAdmin = "SyncAdmin";
}
