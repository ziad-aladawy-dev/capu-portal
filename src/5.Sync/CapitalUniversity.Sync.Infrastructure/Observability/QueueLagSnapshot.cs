namespace CapitalUniversity.Sync.Infrastructure.Observability;

/// <summary>
/// Point-in-time snapshot of one Hangfire queue's depth + oldest-pending age.
/// Phase 10 observability surface — returned by the queue-lag admin endpoint
/// and exposable to a metrics dashboard.
/// </summary>
public sealed class QueueLagSnapshot
{
    public required string Queue { get; init; }

    /// <summary>Number of jobs currently in <c>EnqueuedState</c> waiting for a worker.</summary>
    public required int EnqueuedCount { get; init; }

    /// <summary>Number of jobs currently in <c>ProcessingState</c> (a worker has them).</summary>
    public required int ProcessingCount { get; init; }

    /// <summary>UTC of the oldest job still in <c>EnqueuedState</c>. Null if queue is empty.</summary>
    public DateTimeOffset? OldestEnqueuedAt { get; init; }

    /// <summary>Computed at snapshot time: how long the oldest job has been waiting.</summary>
    public TimeSpan? OldestAge { get; init; }
}