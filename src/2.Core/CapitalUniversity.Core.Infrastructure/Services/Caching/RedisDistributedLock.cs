using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using StackExchange.Redis;

namespace CapitalUniversity.Core.Infrastructure.Services.Caching;

/// <summary>
/// Redis-backed <see cref="IDistributedLock"/> — the shared coordination layer
/// that makes cache rebuilds single-flight across every instance behind the load
/// balancer.
///
/// <para>
/// Acquire = <c>SET key token NX PX ttl</c> (atomic "set if absent with expiry"),
/// so exactly one instance wins and the lock self-heals via the TTL if the holder
/// crashes. Release = a Lua compare-and-delete that only removes the key when the
/// stored value still equals our token, so an expired-then-retaken lock is never
/// released out from under its new owner.
/// </para>
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    // Atomic ownership-checked release. KEYS[1] = lock key, ARGV[1] = our token.
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IAppLogger? _logger;

    public RedisDistributedLock(IConnectionMultiplexer multiplexer, IAppLogger? logger = null)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string resource,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var db = _multiplexer.GetDatabase();
        // Unique per acquisition — proves ownership at release time.
        var token = Guid.NewGuid().ToString("N");

        // SET NX PX — atomic. Throws if Redis is unreachable; the caller
        // (StampedeProtectedCacheService) treats a throw as "coordination
        // unavailable" and degrades to an unprotected rebuild.
        var acquired = await db.StringSetAsync(resource, token, ttl, When.NotExists);
        if (!acquired)
        {
            return null;
        }

        return new RedisLockHandle(db, resource, token, _logger);
    }

    private sealed class RedisLockHandle : IDistributedLockHandle
    {
        private readonly IDatabase _db;
        private readonly string _resource;
        private readonly string _token;
        private readonly IAppLogger? _logger;
        private int _disposed;

        public RedisLockHandle(IDatabase db, string resource, string token, IAppLogger? logger)
        {
            _db = db;
            _resource = resource;
            _token = token;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try
            {
                // Ownership-verified delete — only releases a lock we still hold.
                await _db.ScriptEvaluateAsync(
                    ReleaseScript,
                    new RedisKey[] { _resource },
                    new RedisValue[] { _token });
            }
            catch (Exception ex)
            {
                // Release is best-effort: if it fails the TTL still reaps the
                // lock. Never throw out of dispose.
                await TryLogAsync($"Redis lock release failed for '{_resource}'", ex);
            }
        }

        private async Task TryLogAsync(string message, Exception ex)
        {
            if (_logger is null) return;
            try
            {
                await _logger.LogWarningAsync(message, "RedisDistributedLock", null,
                    new Dictionary<string, object>
                    {
                        { "Exception", ex.GetType().Name },
                        { "Message", ex.Message ?? string.Empty },
                    });
            }
            catch
            {
                // Don't cascade a logger failure.
            }
        }
    }
}
