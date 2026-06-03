using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

/// <summary>
/// Decorator that caches <see cref="ISessionVersionService"/> lookups through
/// <see cref="ICacheService"/>. Reduces DB round-trips on the hot auth path
/// (SessionVersionMiddleware hits this on every authenticated request).
///
/// <para>
/// Invalidation is write-through: <see cref="IncrementVersionAsync"/> drops
/// the cached entry before returning, so the next reader resolves the new
/// value from the database. TTL only bounds blast radius when an instance
/// misses the invalidate (e.g. transient cache transport failure).
/// </para>
///
/// <para>
/// "User not found" is also cached (as <c>Version = null</c>) so that bogus
/// IDs cannot DoS the database with repeated misses.
/// </para>
/// </summary>
public class CachedSessionVersionService : ISessionVersionService
{
    internal const string KeyPrefix = "session-version:";

    private readonly ISessionVersionService _inner;
    private readonly ICacheService _cache;
    private readonly SessionVersionCacheOptions _options;

    public CachedSessionVersionService(
        ISessionVersionService inner,
        ICacheService cache,
        IOptions<SessionVersionCacheOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<int?> GetCurrentVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await _inner.GetCurrentVersionAsync(userId, cancellationToken);
        }

        // Stampede-protected read-through (hot auth-middleware path). The factory
        // always returns a non-null SessionVersionEntry — even for "user not
        // found" (Version = null) — so the negative-cache entry is still stored
        // (GetOrSetAsync only skips caching when the value itself is null).
        var entry = await _cache.GetOrSetAsync<SessionVersionEntry>(
            key: BuildKey(userId),
            factory: async ct => new SessionVersionEntry
            {
                Version = await _inner.GetCurrentVersionAsync(userId, ct),
            },
            expirationTime: TimeSpan.FromMinutes(Math.Max(1, _options.TtlMinutes)),
            cancellationToken: cancellationToken);

        return entry?.Version;
    }

    public async Task<int?> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var newVersion = await _inner.IncrementVersionAsync(userId, cancellationToken);
        // Invalidate after the write so a concurrent reader cannot repopulate
        // the cache with the stale value.
        await _cache.RemoveAsync(BuildKey(userId), cancellationToken);
        return newVersion;
    }

    public async Task InvalidateCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // P2.6 — call on user creation/update to evict the negative-cached
        // "user not found" entry. Otherwise the first auth request after
        // sign-up sees `null` until the TTL expires.
        await _cache.RemoveAsync(BuildKey(userId), cancellationToken);
        // Forward to inner too so any composite decorator stays in sync.
        await _inner.InvalidateCacheAsync(userId, cancellationToken);
    }

    internal static string BuildKey(Guid userId) => $"{KeyPrefix}{userId:N}";

    public sealed class SessionVersionEntry
    {
        public int? Version { get; set; }
    }
}
