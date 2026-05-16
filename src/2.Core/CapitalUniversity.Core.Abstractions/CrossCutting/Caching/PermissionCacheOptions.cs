namespace CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

/// <summary>
/// Permission-lookup cache tuning, bound from <c>Caching:Permissions</c> in config.
/// Both TTLs are upper bounds — invalidation via per-user version stamps orphans
/// stale entries immediately (no need to wait for expiry).
/// </summary>
public class PermissionCacheOptions
{
    public const string SectionName = "Caching:Permissions";

    /// <summary>
    /// How long a resolved permission set stays cacheable per (user, version, scope).
    /// Default 20 minutes — long enough to amortize the DB lookup across a burst of
    /// requests, short enough to bound the blast radius if version-stamp rotation
    /// somehow misses an instance (e.g. transient Redis write loss).
    /// </summary>
    public int LookupTtlMinutes { get; set; } = 20;

    /// <summary>
    /// How long the per-user version stamp lives. Default 24 hours. The stamp is
    /// re-seeded on the next read if missing, so this just bounds memory pressure
    /// for users with no recent activity.
    /// </summary>
    public int VersionTtlHours { get; set; } = 24;
}
