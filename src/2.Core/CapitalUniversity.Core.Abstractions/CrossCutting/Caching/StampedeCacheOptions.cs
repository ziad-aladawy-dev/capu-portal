namespace CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

/// <summary>
/// Tunables for the cache-stampede protection wrapper. Bound from
/// <c>Caching:Stampede</c>.
///
/// <para>
/// The wrapper turns a cache miss into a single-flight rebuild: one instance
/// holds a distributed lock and runs the factory, every other instance polls the
/// cache until the value appears (or a bounded timeout elapses, after which it
/// degrades to running the factory itself rather than blocking forever).
/// </para>
/// </summary>
public class StampedeCacheOptions
{
    public const string SectionName = "Caching:Stampede";

    /// <summary>
    /// When false the wrapper becomes a plain read-through (no locking, no
    /// polling) — useful for diagnostics without changing the registration.
    /// Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// TTL on the per-key rebuild lock — the deadlock backstop if the holder
    /// crashes before releasing. Must comfortably exceed the slowest factory
    /// (DB) execution. Default 30 seconds.
    /// </summary>
    public int LockTtlSeconds { get; set; } = 30;

    /// <summary>
    /// Upper bound a non-holder will wait (polling the cache) for the holder to
    /// publish the rebuilt value. On timeout it degrades to running the factory
    /// itself, so this also bounds worst-case latency. Default 5 seconds.
    /// </summary>
    public int MaxWaitMilliseconds { get; set; } = 5000;

    /// <summary>
    /// How often a waiting non-holder re-checks the cache. Smaller = lower
    /// latency once the value lands, higher = less Redis chatter. Default 50ms.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 50;
}
