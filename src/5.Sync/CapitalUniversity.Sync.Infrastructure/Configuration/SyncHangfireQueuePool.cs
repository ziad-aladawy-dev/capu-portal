namespace CapitalUniversity.Sync.Infrastructure.Configuration;

/// <summary>
/// One Hangfire <c>BackgroundJobServer</c> instance. Each pool owns a disjoint
/// subset of <see cref="SyncHangfireOptions.Queues"/> with its own worker count.
///
/// <para>
/// Hangfire's <c>BackgroundJobServer</c> assigns a single shared worker pool per
/// instance; a server listening on <c>[a, b]</c> with WorkerCount=4 has 4 workers
/// total split across both queues, FIFO. Per-queue dedicated workers therefore
/// require multiple server instances. This config carries the per-instance shape.
/// </para>
///
/// <para>
/// When <see cref="SyncHangfireOptions.QueuePools"/> is empty, the host falls back
/// to a single legacy <c>BackgroundJobServer</c> covering all
/// <see cref="SyncHangfireOptions.Queues"/> with <see cref="SyncHangfireOptions.WorkerCount"/>.
/// </para>
/// </summary>
public sealed class SyncHangfireQueuePool
{
    /// <summary>
    /// Friendly name surfaced as <c>BackgroundJobServerOptions.ServerName</c>. Defaults
    /// to the comma-joined queue list if omitted.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Queues the pool listens on. MUST be a subset of
    /// <see cref="SyncHangfireOptions.Queues"/>; the startup validator enforces this.
    /// MUST be disjoint from every other pool's queues so a queue is not double-served.
    /// </summary>
    public List<string> Queues { get; set; } = new();

    /// <summary>
    /// Worker count for this pool. <see cref="SyncHangfireOptions.WorkerCount"/> only
    /// applies to the single-server fallback; per-pool counts override.
    /// </summary>
    public int WorkerCount { get; set; } = 4;
}