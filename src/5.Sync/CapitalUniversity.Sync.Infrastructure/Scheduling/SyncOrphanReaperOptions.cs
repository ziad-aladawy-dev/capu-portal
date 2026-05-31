namespace CapitalUniversity.Sync.Infrastructure.Scheduling;

/// <summary>
/// Phase X hardening fix #5 configuration. Wires the long-existing-but-unused
/// <c>ISyncRunRepository.FindOrphanRunsAsync</c> to a recurring sweeper that
/// transitions stranded <c>Enqueued</c> rows (no <c>HangfireJobId</c>) to
/// <c>Failed</c> after a grace window.
///
/// <para>
/// Orphan condition: <c>sync.runs.Status = Enqueued AND HangfireJobId IS NULL
/// AND EnqueuedAt &lt; now - <see cref="GraceMinutes"/></c>. The grace window
/// protects against the race where the dispatcher has opened the run row but
/// hasn't yet linked the Hangfire job id (a normally-sub-second window).
/// </para>
/// </summary>
public sealed class SyncOrphanReaperOptions
{
    public const string SectionName = "Sync:OrphanReaper";

    /// <summary>Operator opt-in. Default <c>true</c> — orphans are silent failures
    /// otherwise.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cron expression. Default every 5 minutes.</summary>
    public string CronExpression { get; set; } = "*/5 * * * *";

    /// <summary>Grace window before an Enqueued+null-JobId row is considered orphaned.
    /// Default 10 minutes — comfortably longer than the dispatcher's enqueue
    /// transaction window.</summary>
    public int GraceMinutes { get; set; } = 10;

    /// <summary>Per-sweep cap so a backlog drain is bounded.</summary>
    public int MaxReapedPerRun { get; set; } = 1000;
}