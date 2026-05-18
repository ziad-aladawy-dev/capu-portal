using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Services.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

/// <summary>
/// SessionVersion cache decorator contract: serves repeated reads from cache,
/// caches "user not found" negative hits, invalidates write-through on
/// increment, and becomes a pass-through when disabled.
/// </summary>
public class CachedSessionVersionServiceTests
{
    private static IOptions<SessionVersionCacheOptions> Opts(bool enabled = true, int ttl = 5) =>
        Options.Create(new SessionVersionCacheOptions { Enabled = enabled, TtlMinutes = ttl });

    private sealed class StubCache : ICacheService
    {
        private readonly Dictionary<string, object?> _store = new();
        public int GetCalls;
        public int SetCalls;
        public int RemoveCalls;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            SetCalls++;
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GetCurrentVersion_FirstCallMisses_HitsInnerAndCaches()
    {
        var inner = new Mock<ISessionVersionService>();
        var userId = Guid.NewGuid();
        inner.Setup(s => s.GetCurrentVersionAsync(userId, default)).ReturnsAsync(7);
        var cache = new StubCache();

        var sut = new CachedSessionVersionService(inner.Object, cache, Opts());

        var result = await sut.GetCurrentVersionAsync(userId);

        result.Should().Be(7);
        cache.SetCalls.Should().Be(1);
        inner.Verify(s => s.GetCurrentVersionAsync(userId, default), Times.Once);
    }

    [Fact]
    public async Task GetCurrentVersion_SecondCall_ServedFromCache_InnerNotCalled()
    {
        var inner = new Mock<ISessionVersionService>();
        var userId = Guid.NewGuid();
        inner.Setup(s => s.GetCurrentVersionAsync(userId, default)).ReturnsAsync(7);
        var cache = new StubCache();
        var sut = new CachedSessionVersionService(inner.Object, cache, Opts());

        await sut.GetCurrentVersionAsync(userId);
        var second = await sut.GetCurrentVersionAsync(userId);

        second.Should().Be(7);
        inner.Verify(s => s.GetCurrentVersionAsync(userId, default), Times.Once);
    }

    [Fact]
    public async Task GetCurrentVersion_NullFromInner_IsCached_NoSecondLookup()
    {
        var inner = new Mock<ISessionVersionService>();
        var userId = Guid.NewGuid();
        inner.Setup(s => s.GetCurrentVersionAsync(userId, default)).ReturnsAsync((int?)null);
        var cache = new StubCache();
        var sut = new CachedSessionVersionService(inner.Object, cache, Opts());

        var first = await sut.GetCurrentVersionAsync(userId);
        var second = await sut.GetCurrentVersionAsync(userId);

        first.Should().BeNull();
        second.Should().BeNull();
        inner.Verify(s => s.GetCurrentVersionAsync(userId, default), Times.Once,
            "negative hits must be cached so bogus IDs cannot hammer the DB");
    }

    [Fact]
    public async Task Increment_InvalidatesCachedEntry()
    {
        var inner = new Mock<ISessionVersionService>();
        var userId = Guid.NewGuid();
        inner.SetupSequence(s => s.GetCurrentVersionAsync(userId, default))
             .ReturnsAsync(7)
             .ReturnsAsync(8);
        inner.Setup(s => s.IncrementVersionAsync(userId, default)).ReturnsAsync(8);
        var cache = new StubCache();
        var sut = new CachedSessionVersionService(inner.Object, cache, Opts());

        var before = await sut.GetCurrentVersionAsync(userId);
        var bumped = await sut.IncrementVersionAsync(userId);
        var after = await sut.GetCurrentVersionAsync(userId);

        before.Should().Be(7);
        bumped.Should().Be(8);
        after.Should().Be(8, "cache must have been invalidated by Increment");
        cache.RemoveCalls.Should().Be(1);
        inner.Verify(s => s.GetCurrentVersionAsync(userId, default), Times.Exactly(2));
    }

    [Fact]
    public async Task Disabled_BehavesAsPassthrough()
    {
        var inner = new Mock<ISessionVersionService>();
        var userId = Guid.NewGuid();
        inner.Setup(s => s.GetCurrentVersionAsync(userId, default)).ReturnsAsync(3);
        var cache = new StubCache();
        var sut = new CachedSessionVersionService(inner.Object, cache, Opts(enabled: false));

        await sut.GetCurrentVersionAsync(userId);
        await sut.GetCurrentVersionAsync(userId);

        cache.GetCalls.Should().Be(0);
        cache.SetCalls.Should().Be(0);
        inner.Verify(s => s.GetCurrentVersionAsync(userId, default), Times.Exactly(2));
    }
}
