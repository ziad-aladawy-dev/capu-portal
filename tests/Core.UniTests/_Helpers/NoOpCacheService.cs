using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

namespace CapitalUniversity.Core.UniTests._Helpers;

/// <summary>
/// Pass-through <see cref="ICacheService"/> for unit tests that exercise service
/// logic without a caching layer. Every read is a miss and every write is a
/// no-op, so the default <c>GetOrSetAsync</c> simply runs the factory each call —
/// service behaviour is identical to "no cache configured". Use this when a test
/// asserts repository call counts and must not be perturbed by caching.
/// </summary>
internal sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
