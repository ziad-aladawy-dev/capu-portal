namespace CapitalUniversity.Sync.Infrastructure.Configuration;

public sealed class SyncHangfireOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string SchemaName { get; set; } = "HangFire";

    public bool PrepareSchemaIfNecessary { get; set; } = true;

    /// <summary>
    /// Full set of queues the host is expected to drain. Every queue routed via
    /// <see cref="SyncOptions.ModuleQueues"/> must appear here (enforced by
    /// <c>SyncQueueConfigurationValidator</c>). When <see cref="QueuePools"/> is empty,
    /// the host listens on all of these in a single shared worker pool; when pools
    /// are configured, the validator additionally requires that every queue in
    /// <see cref="QueuePools"/> appears here and that pools are pairwise disjoint.
    /// </summary>
    public List<string> Queues { get; set; } = new() { "default" };

    /// <summary>
    /// Worker count for the single-server fallback (used when <see cref="QueuePools"/>
    /// is empty). Per-pool worker counts come from <see cref="SyncHangfireQueuePool.WorkerCount"/>.
    /// </summary>
    public int? WorkerCount { get; set; }

    /// <summary>
    /// Optional per-queue worker pools (Phase 8). Each pool registers a dedicated
    /// <c>BackgroundJobServer</c> instance — preventing one queue's backlog from
    /// starving workers off another queue. Empty list = legacy single-server mode.
    /// </summary>
    public List<SyncHangfireQueuePool> QueuePools { get; set; } = new();

    // ── Phase 8 dispatch-tuning knobs (lifted from hardcoded Program.cs values) ──

    /// <summary>
    /// How often Hangfire polls SQL for newly-enqueued jobs. Lower = faster pickup,
    /// higher = less SQL chatter. Default 2s matches the prior hardcoded value.
    /// </summary>
    public TimeSpan QueuePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Upper bound on a single Hangfire SQL command batch. Default 5min.
    /// </summary>
    public TimeSpan CommandBatchMaxTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Wait before Hangfire considers a fetched job "invisible-expired" and lets
    /// another worker re-fetch it. Should comfortably exceed the longest expected
    /// job execution time. Default 5min.
    /// </summary>
    public TimeSpan SlidingInvisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often Hangfire's expiration sweeper runs (deletes Succeeded/Failed jobs
    /// past their state-bound TTL). Default 30min.
    /// </summary>
    public TimeSpan JobExpirationCheckInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Hangfire's <c>BackgroundJobServer.CancellationCheckInterval</c>. Lower =
    /// faster response to <c>BackgroundJob.Delete</c>. Default 1s.
    /// </summary>
    public TimeSpan ServerCancellationCheckInterval { get; set; } = TimeSpan.FromSeconds(1);
}