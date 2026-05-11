using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace CapitalUniversity.Core.Application.CrossCutting.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;

    public RedisCacheService(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cachedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(cachedValue))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(cachedValue);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expirationTime.HasValue)
        {
            options.SetAbsoluteExpiration(expirationTime.Value);
        }

        var serializedValue = JsonSerializer.Serialize(value);
        await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _distributedCache.RemoveAsync(key, cancellationToken);
    }
}
