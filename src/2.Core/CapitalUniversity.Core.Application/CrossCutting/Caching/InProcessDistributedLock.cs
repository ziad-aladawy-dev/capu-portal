using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;

namespace CapitalUniversity.Core.Application.CrossCutting.Caching;

/// <summary>
/// Single-process fallback for <see cref="IDistributedLock"/>, used when the
/// system runs against the in-memory cache (no Redis). There is only one
/// instance, so a per-key <see cref="SemaphoreSlim"/> gives the same
/// single-flight guarantee the Redis lock gives across instances.
///
/// <para>
/// Acquisition is non-blocking (<c>WaitAsync(0)</c>): the first caller takes the
/// gate, concurrent callers get <c>null</c> and fall through to polling the
/// cache — mirroring the distributed semantics so the wrapper behaves the same
/// in both modes. The TTL is ignored: a single process always reaches the
/// <c>finally</c> that releases, so there is no cross-instance crash to guard
/// against.
/// </para>
/// </summary>
public sealed class InProcessDistributedLock : IDistributedLock
{
    // One gate per resource key. Bounded in practice by the set of distinct
    // cache keys (course ids, plan ids, …); a registered singleton so the gates
    // are shared across every scoped cache instance in the process.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public Task<IDistributedLockHandle?> TryAcquireAsync(
        string resource,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gate = _gates.GetOrAdd(resource, static _ => new SemaphoreSlim(1, 1));

        // Zero-timeout: take it now or report contention immediately.
        if (!gate.Wait(0, cancellationToken))
        {
            return Task.FromResult<IDistributedLockHandle?>(null);
        }

        return Task.FromResult<IDistributedLockHandle?>(new Handle(gate));
    }

    private sealed class Handle : IDistributedLockHandle
    {
        private SemaphoreSlim? _gate;

        public Handle(SemaphoreSlim gate) => _gate = gate;

        public ValueTask DisposeAsync()
        {
            // Guard against double-dispose releasing the gate twice.
            var gate = Interlocked.Exchange(ref _gate, null);
            gate?.Release();
            return ValueTask.CompletedTask;
        }
    }
}
