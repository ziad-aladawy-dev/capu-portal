using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using Hangfire;

namespace CapitalUniversity.Sync.Host.Scheduling;

/// <summary>
/// Thin Hangfire entry point used by every recurring sync job. Responsibility:
///   • selecting module
///   • selecting direction
///   • staggering the dispatch within the firing minute
///   • calling the dispatcher
/// No business logic. No enrichment beyond the trigger source.
///
/// <para>
/// <b>Cron-storm mitigation.</b> Hangfire 5-field cron has no sub-minute
/// precision, so all 10 module triggers fire at :00 of every minute. Without
/// staggering, the dispatcher takes 10 simultaneous OpenRunAsync + Hangfire
/// enqueue round-trips at the same second — a synchronous load spike on both
/// the sync audit DB and the Hangfire SQL store, repeating every 60 seconds.
/// This trigger inserts a deterministic per-(module, direction) jitter (0 to
/// <see cref="StaggerSeconds"/>) before dispatching, spreading the 10 cron
/// hits evenly across the firing minute. The hash is stable across process
/// restarts so the same (module, direction) pair always lands on the same
/// offset — observable in audit logs.
/// </para>
/// </summary>
public sealed class SyncRecurringTrigger
{
    private const string TriggerSource = "scheduled";

    /// <summary>
    /// Maximum jitter window. Capped well under 60 so the offset never bleeds
    /// into the next minute's cron tick. With 10 module triggers spread over
    /// 50 seconds, the expected gap between consecutive dispatches is ~5 sec.
    /// </summary>
    public const int StaggerSeconds = 50;

    private readonly ISyncDispatcher _dispatcher;

    public SyncRecurringTrigger(ISyncDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task TriggerAsync(string moduleName, SyncDirection direction)
    {
        // Stagger by a deterministic per-(module, direction) offset so the 10
        // minutely sync triggers don't all dispatch at :00 of every minute.
        // See class-level doc for rationale.
        var jitterSeconds = ComputeStaggerSeconds(moduleName, direction);
        await Task.Delay(TimeSpan.FromSeconds(jitterSeconds), CancellationToken.None)
            .ConfigureAwait(false);

        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = TriggerSource
        };

        await _dispatcher.DispatchAsync(moduleName, direction, metadata, CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deterministic 0…<see cref="StaggerSeconds"/>-1 jitter offset derived
    /// from a stable hash of the (module, direction) pair. Same input always
    /// yields the same offset across process restarts so operators see a
    /// predictable per-job dispatch second in audit logs.
    /// <para>
    /// FNV-1a 32-bit. Cryptographic strength isn't needed — only stability
    /// and roughly uniform distribution across 10 inputs.
    /// </para>
    /// </summary>
    public static int ComputeStaggerSeconds(string moduleName, SyncDirection direction)
    {
        var seed = (moduleName ?? string.Empty) + ":" + direction;
        unchecked
        {
            uint hash = 2166136261u;
            foreach (var c in seed)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)(hash % (uint)StaggerSeconds);
        }
    }
}
