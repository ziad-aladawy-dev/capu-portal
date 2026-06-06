using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

namespace CapitalUniversity.Core.Application.CrossCutting.Caching;

/// <summary>
/// Cache-stampede protection wrapper around an inner <see cref="ICacheService"/>.
///
/// <para>
/// <c>GetAsync</c>/<c>SetAsync</c>/<c>RemoveAsync</c> pass straight through, so
/// existing cache-aside call sites are unchanged. The protection lives in
/// <see cref="GetOrSetAsync{T}"/>: on a miss, exactly one instance across the
/// whole deployment acquires a per-key distributed lock and rebuilds the entry,
/// while every other instance polls the cache and reuses the published result.
/// </para>
///
/// <para>
/// Flow (per the spec):
/// <list type="number">
///   <item>Read the cache. Hit → return immediately.</item>
///   <item>Miss → try to acquire the per-key lock.</item>
///   <item>Lock acquired → <b>double-check</b> the cache (close the race where a
///         neighbour published between our read and our lock), then run the
///         factory, store with TTL, and release the lock (ownership-verified).</item>
///   <item>Lock not acquired → poll the cache up to a bounded timeout. Value
///         appears → return it. Timeout → run the factory ourselves (safe
///         degradation; never block forever).</item>
///   <item>Coordination unavailable (Redis down → lock throws) → run the factory
///         directly. A Redis outage degrades to "no stampede protection", never
///         a failed request.</item>
/// </list>
/// </para>
/// </summary>
public sealed class StampedeProtectedCacheService : ICacheService
{
    private const string LockKeyPrefix = "stampede-lock:";

    private readonly ICacheService _inner;
    private readonly IDistributedLock _lock;
    private readonly StampedeCacheOptions _options;
    private readonly IAppLogger? _logger;

    public StampedeProtectedCacheService(
        ICacheService inner,
        IDistributedLock distributedLock,
        StampedeCacheOptions options,
        IAppLogger? logger = null)
    {
        _inner = inner;
        _lock = distributedLock;
        _options = options;
        _logger = logger;
    }

    // Transport operations are pure pass-through.
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => _inner.GetAsync<T>(key, cancellationToken);

    public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        => _inner.SetAsync(key, value, expirationTime, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => _inner.RemoveAsync(key, cancellationToken);

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expirationTime = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Fast path — value already cached.
        var cached = await _inner.GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        // Kill-switch: behave as a plain (unprotected) read-through.
        if (!_options.Enabled)
        {
            return await BuildAndStoreAsync(key, factory, expirationTime, cancellationToken);
        }

        // 2. Miss — try to become the single rebuilder for this key.
        var lockKey = LockKeyPrefix + key;
        var lockTtl = TimeSpan.FromSeconds(Math.Max(1, _options.LockTtlSeconds));

        IDistributedLockHandle? handle;
        try
        {
            handle = await _lock.TryAcquireAsync(lockKey, lockTtl, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 5. Coordination layer unavailable — degrade to a direct rebuild.
            await TryLogAsync($"Stampede lock acquire failed for key '{key}'; running factory unprotected", ex);
            return await BuildAndStoreAsync(key, factory, expirationTime, cancellationToken);
        }

        if (handle is not null)
        {
            // 3. We own the rebuild.
            await using (handle)
            {
                // Double-check: a neighbour may have published between step 1 and
                // taking the lock.
                var recheck = await _inner.GetAsync<T>(key, cancellationToken);
                if (recheck is not null) return recheck;

                return await BuildAndStoreAsync(key, factory, expirationTime, cancellationToken);
            }
        }

        // 4. Someone else is rebuilding — poll the cache, then fall back.
        return await WaitForValueOrBuildAsync(key, factory, expirationTime, cancellationToken);
    }

    /// <summary>
    /// Run the factory and cache a non-null result. The single place the factory
    /// executes, so caching policy (skip nulls) lives in one spot.
    /// </summary>
    private async Task<T?> BuildAndStoreAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expirationTime,
        CancellationToken cancellationToken)
    {
        var value = await factory(cancellationToken);
        if (value is not null)
        {
            await _inner.SetAsync(key, value, expirationTime, cancellationToken);
        }

        return value;
    }

    /// <summary>
    /// Poll the cache for the value another instance is rebuilding, up to
    /// <see cref="StampedeCacheOptions.MaxWaitMilliseconds"/>. On timeout, degrade
    /// to a direct rebuild so the caller never blocks indefinitely.
    /// </summary>
    private async Task<T?> WaitForValueOrBuildAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expirationTime,
        CancellationToken cancellationToken)
    {
        var pollInterval = Math.Max(1, _options.PollIntervalMilliseconds);
        var maxWait = Math.Max(0, _options.MaxWaitMilliseconds);
        // Bounded number of polls — no clock dependency, no infinite loop.
        var attempts = maxWait / pollInterval;

        for (var i = 0; i < attempts; i++)
        {
            await Task.Delay(pollInterval, cancellationToken);

            var polled = await _inner.GetAsync<T>(key, cancellationToken);
            if (polled is not null) return polled;
        }

        // Timeout — safe degradation. We do NOT take the lock here: the holder is
        // (probably) still working; running the factory ourselves is the bounded
        // worst case, not the steady state.
        await TryLogAsync(
            $"Stampede wait timed out for key '{key}' after {maxWait}ms; running factory unprotected",
            exception: null);
        return await BuildAndStoreAsync(key, factory, expirationTime, cancellationToken);
    }

    private async Task TryLogAsync(string message, Exception? exception)
    {
        if (_logger is null) return;
        try
        {
            var metadata = new Dictionary<string, object>();
            if (exception is not null)
            {
                metadata["Exception"] = exception.GetType().Name;
                metadata["Message"] = exception.Message ?? string.Empty;
            }

            await _logger.LogWarningAsync(message, "StampedeProtectedCacheService", null, metadata);
        }
        catch
        {
            // Logger itself failed — never cascade out of the cache layer.
        }
    }
}
