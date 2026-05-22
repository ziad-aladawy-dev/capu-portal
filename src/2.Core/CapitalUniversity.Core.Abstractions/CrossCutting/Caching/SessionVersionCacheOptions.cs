namespace CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

/// <summary>
/// Tunables for the <c>ISessionVersionService</c> cache decorator. Bound from
/// <c>Caching:SessionVersion</c>.
///
/// <para>
/// The decorator caches per-user SessionVersion lookups so the auth middleware
/// can short-circuit a DB round-trip on every authenticated request. The cache
/// is write-through invalidated on increment, so the TTL is an upper bound and
/// not the correctness boundary.
/// </para>
/// </summary>
public class SessionVersionCacheOptions
{
    public const string SectionName = "Caching:SessionVersion";

    /// <summary>
    /// When false, the decorator becomes a pass-through and never reads or
    /// writes the cache. Defaults to true; flip to false to short-circuit the
    /// cache layer (e.g. for diagnostics) without removing the registration.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Absolute TTL for a cached SessionVersion entry. Increment invalidates
    /// the entry directly, so this only matters when an instance somehow
    /// misses the invalidate (transient Redis write loss). Default 5 minutes.
    /// </summary>
    public int TtlMinutes { get; set; } = 5;
}
