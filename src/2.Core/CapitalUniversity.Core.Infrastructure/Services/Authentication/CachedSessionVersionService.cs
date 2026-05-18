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

        var key = BuildKey(userId);
        var cached = await _cache.GetAsync<SessionVersionEntry>(key, cancellationToken);
        if (cached is not null)
        {
            return cached.Version;
        }

        var fresh = await _inner.GetCurrentVersionAsync(userId, cancellationToken);
        await _cache.SetAsync(
            key,
            new SessionVersionEntry { Version = fresh },
            TimeSpan.FromMinutes(Math.Max(1, _options.TtlMinutes)),
            cancellationToken);
        return fresh;
    }

    public async Task<int?> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var newVersion = await _inner.IncrementVersionAsync(userId, cancellationToken);
        // Invalidate after the write so a concurrent reader cannot repopulate
        // the cache with the stale value.
        await _cache.RemoveAsync(BuildKey(userId), cancellationToken);
        return newVersion;
    }

    internal static string BuildKey(Guid userId) => $"{KeyPrefix}{userId:N}";

    public sealed class SessionVersionEntry
    {
        public int? Version { get; set; }
    }
}
