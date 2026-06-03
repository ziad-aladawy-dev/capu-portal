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

    /// <summary>
    /// Cron expression. Default every 5 minutes at minute :02, :07, :12, …
    /// — offset off the :00 boundary so the reaper sweep doesn't collide
    /// with the 10 module-sync cron triggers (all of which fire at :00 every
    /// minute). Sub-minute spread of those triggers is handled by
    /// <c>SyncRecurringTrigger.ComputeStaggerSeconds</c>.
    /// </summary>
    public string CronExpression { get; set; } = "2-57/5 * * * *";

    /// <summary>Grace window before an Enqueued+null-JobId row is considered orphaned.
    /// Default 10 minutes — comfortably longer than the dispatcher's enqueue
    /// transaction window.</summary>
    public int GraceMinutes { get; set; } = 10;

    /// <summary>Per-sweep cap so a backlog drain is bounded.</summary>
    public int MaxReapedPerRun { get; set; } = 1000;
}