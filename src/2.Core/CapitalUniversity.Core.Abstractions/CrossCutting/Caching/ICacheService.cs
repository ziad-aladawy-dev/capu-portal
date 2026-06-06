using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-through helper: return the cached value for <paramref name="key"/>, or
    /// run <paramref name="factory"/>, cache its (non-null) result, and return it.
    ///
    /// <para>
    /// The decorated implementation (see the stampede-protection wrapper) makes
    /// this single-flight across a multi-instance deployment: under a burst of
    /// concurrent misses for the same key, only one factory runs and the rest
    /// reuse its result. <c>null</c> results are not cached (no negative caching),
    /// matching the cache-aside call sites this replaces.
    /// </para>
    ///
    /// <para>
    /// The default implementation is a plain, unprotected read-through so any
    /// bare <see cref="ICacheService"/> still satisfies the contract; the
    /// stampede guarantees only hold when the wrapper is registered.
    /// </para>
    /// </summary>
    async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expirationTime = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        var value = await factory(cancellationToken);
        if (value is not null)
        {
            await SetAsync(key, value, expirationTime, cancellationToken);
        }

        return value;
    }
}