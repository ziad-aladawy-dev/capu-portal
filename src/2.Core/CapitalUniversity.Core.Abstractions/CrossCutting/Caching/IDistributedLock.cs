using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

/// <summary>
/// Shared coordination primitive used by the cache-stampede protection layer.
/// One instance per cache miss burst acquires the lock and rebuilds the entry;
/// the others wait and reuse the result.
///
/// <para>
/// Implementations must use an atomic "set if not exists with expiry" so the
/// lock cannot be held forever if the holder crashes (the TTL is the deadlock
/// backstop). Acquisition is non-blocking: it either takes the lock now or
/// reports contention immediately so the caller can poll the cache instead.
/// </para>
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Try to take the lock for <paramref name="resource"/> exactly once, without
    /// blocking. Returns a handle when acquired (dispose it to release), or
    /// <c>null</c> when another instance currently holds it.
    /// </summary>
    /// <param name="resource">Namespaced lock key, derived from the cache key.</param>
    /// <param name="ttl">
    /// How long the lock survives if never released — the deadlock backstop.
    /// Should comfortably exceed the expected factory (DB) execution time.
    /// </param>
    /// <exception cref="Exception">
    /// May throw if the coordination layer (e.g. Redis) is unreachable. Callers
    /// treat a throw as "coordination unavailable" and fall back to running the
    /// factory directly (safe degradation).
    /// </exception>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string resource,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ownership token for a held <see cref="IDistributedLock"/>. Disposing releases
/// the lock <em>only if this handle still owns it</em> (the value the handle
/// wrote is still the value stored), so a lock that already expired and was
/// re-taken by another instance is never released out from under its new owner.
/// Release is best-effort and never throws.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable
{
}
