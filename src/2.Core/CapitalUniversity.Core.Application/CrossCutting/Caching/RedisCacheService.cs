using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using Microsoft.Extensions.Caching.Distributed;

namespace CapitalUniversity.Core.Application.CrossCutting.Caching;

/// <summary>
/// Distributed cache backed by Redis (via <c>IDistributedCache</c>).
///
/// Failure mode: every operation catches transport / deserialization errors and
/// treats them as a cache miss (or silent no-op for writes). A Redis outage must
/// never turn into a 500 — it should degrade the system to "no cache" performance
/// without breaking correctness.
/// </summary>
public class RedisCacheService : ICacheService
{
    // Keep default JSON options (PascalCase, no special handling) so round-trips
    // match the rest of the codebase and prior cached entries remain readable.
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IDistributedCache _distributedCache;
    private readonly IAppLogger? _logger;

    public RedisCacheService(IDistributedCache distributedCache, IAppLogger? logger = null)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(cachedValue))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cachedValue, JsonOptions);
        }
        catch (Exception ex)
        {
            await TryLogAsync($"Redis GET failed for key '{key}'", ex);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();
            if (expirationTime.HasValue)
            {
                options.SetAbsoluteExpiration(expirationTime.Value);
            }

            var serializedValue = JsonSerializer.Serialize(value, JsonOptions);
            await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
        }
        catch (Exception ex)
        {
            await TryLogAsync($"Redis SET failed for key '{key}'", ex);
            // Swallow — caching is best-effort, the caller already has the value.
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            await TryLogAsync($"Redis REMOVE failed for key '{key}'", ex);
        }
    }

    private async Task TryLogAsync(string message, Exception ex)
    {
        if (_logger == null) return;
        try
        {
            await _logger.LogWarningAsync(message, "RedisCacheService", null,
                new Dictionary<string, object>
                {
                    { "Exception", ex.GetType().Name },
                    { "Message", ex.Message ?? string.Empty },
                });
        }
        catch
        {
            // Logger itself failed — don't cascade.
        }
    }
}
