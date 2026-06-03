using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Application.CrossCutting.Caching;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Caching;

/// <summary>
/// Behavioural coverage for the cache-stampede protection wrapper, mapped to the
/// acceptance criteria: one rebuild per key per burst, waiters reuse the result,
/// bounded waiting, and safe degradation when the lock layer is down.
/// </summary>
public class StampedeProtectedCacheServiceTests
{
    private static StampedeCacheOptions Options(
        bool enabled = true,
        int lockTtlSeconds = 30,
        int maxWaitMs = 5000,
        int pollMs = 10) => new()
        {
            Enabled = enabled,
            LockTtlSeconds = lockTtlSeconds,
            MaxWaitMilliseconds = maxWaitMs,
            PollIntervalMilliseconds = pollMs,
        };

    [Fact]
    public async Task GetOrSetAsync_WhenValueCached_DoesNotRunFactory()
    {
        var cache = new FakeCache();
        await cache.SetAsync("k", "cached");
        var sut = new StampedeProtectedCacheService(cache, new InProcessDistributedLock(), Options());
        var factoryCalls = 0;

        var result = await sut.GetOrSetAsync<string>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult<string?>("fresh");
        });

        result.Should().Be("cached");
        factoryCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentMissesForSameKey_RunFactoryExactlyOnce()
    {
        // The headline criterion: a burst of concurrent misses for an expired key
        // results in only ONE factory (DB) call; everyone else reuses the result.
        var cache = new FakeCache();
        var sut = new StampedeProtectedCacheService(cache, new InProcessDistributedLock(), Options());
        var factoryCalls = 0;

        async Task<string?> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref factoryCalls);
            await Task.Delay(50, ct); // simulate a slow DB rebuild widening the race
            return "value";
        }

        var callers = Enumerable.Range(0, 50)
            .Select(_ => sut.GetOrSetAsync<string>("hot-key", Factory))
            .ToArray();
        var results = await Task.WhenAll(callers);

        factoryCalls.Should().Be(1);
        results.Should().OnlyContain(r => r == "value");
    }

    [Fact]
    public async Task GetOrSetAsync_WhenLockHeldElsewhere_PollsAndReusesPublishedValue()
    {
        // Lock never granted (another instance owns it). The value lands in the
        // cache mid-poll; the waiter must return it without running the factory.
        var cache = new ValueAppearsAfterCache(appearOnRead: 3, value: "published");
        var sut = new StampedeProtectedCacheService(cache, new NeverAcquiresLock(), Options());
        var factoryCalls = 0;

        var result = await sut.GetOrSetAsync<string>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult<string?>("factory");
        });

        result.Should().Be("published");
        factoryCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenWaitTimesOut_FallsBackToFactory()
    {
        // Lock never granted and the value never appears — the waiter must not
        // block forever; after the bounded wait it rebuilds itself.
        var cache = new FakeCache();
        var sut = new StampedeProtectedCacheService(
            cache, new NeverAcquiresLock(), Options(maxWaitMs: 60, pollMs: 10));
        var factoryCalls = 0;

        var result = await sut.GetOrSetAsync<string>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult<string?>("fallback");
        });

        result.Should().Be("fallback");
        factoryCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrSetAsync_WhenLockLayerThrows_DegradesToDirectFactory()
    {
        // Redis down: acquiring the lock throws. The request must still succeed
        // by running the factory directly (safe degradation, not a 500).
        var cache = new FakeCache();
        var sut = new StampedeProtectedCacheService(cache, new ThrowingLock(), Options());
        var factoryCalls = 0;

        var result = await sut.GetOrSetAsync<string>("k", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult<string?>("degraded");
        });

        result.Should().Be("degraded");
        factoryCalls.Should().Be(1);
        (await cache.GetAsync<string>("k")).Should().Be("degraded"); // still cached
    }

    [Fact]
    public async Task GetOrSetAsync_WhenFactoryReturnsNull_DoesNotCache()
    {
        // No negative caching: a null rebuild is returned but never stored, so a
        // later call re-runs the factory.
        var cache = new FakeCache();
        var sut = new StampedeProtectedCacheService(cache, new InProcessDistributedLock(), Options());
        var factoryCalls = 0;

        Task<string?> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult<string?>(null);
        }

        (await sut.GetOrSetAsync<string>("k", Factory)).Should().BeNull();
        (await sut.GetOrSetAsync<string>("k", Factory)).Should().BeNull();

        factoryCalls.Should().Be(2);
    }

    // --- Test doubles -------------------------------------------------------

    private sealed class FakeCache : ICacheService
    {
        private readonly ConcurrentDictionary<string, object> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) && v is T t ? t : default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            if (value is not null) _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    /// <summary>Returns null until the Nth read, then yields a fixed value.</summary>
    private sealed class ValueAppearsAfterCache : ICacheService
    {
        private readonly int _appearOnRead;
        private readonly string _value;
        private int _reads;

        public ValueAppearsAfterCache(int appearOnRead, string value)
        {
            _appearOnRead = appearOnRead;
            _value = value;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var n = Interlocked.Increment(ref _reads);
            object? result = n >= _appearOnRead ? _value : null;
            return Task.FromResult(result is T t ? t : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NeverAcquiresLock : IDistributedLock
    {
        public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default)
            => Task.FromResult<IDistributedLockHandle?>(null);
    }

    private sealed class ThrowingLock : IDistributedLock
    {
        public Task<IDistributedLockHandle?> TryAcquireAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("redis down");
    }
}
