namespace CapitalUniversity.Sync.Infrastructure.Configuration;

/// <summary>
/// Phase 9 retention configuration. Drives a single Hangfire recurring job that
/// trims four audit tables under the <c>sync</c> schema and any operator-declared
/// outbox tables (per-module, in their own schemas).
///
/// <para>
/// <b>Default Enabled = false.</b> Retention is destructive — operators opt in
/// explicitly. The recurring job is registered regardless so the cron can be
/// observed in the Hangfire dashboard, but a disabled flag short-circuits the
/// DELETE itself.
/// </para>
///
/// <para>
/// Aligned with <c>docs/sync/SyncAuditRetention.md</c> — the long-documented
/// strategy that Phase 9 finally implements.
/// </para>
/// </summary>
public sealed class SyncRetentionOptions
{
    public const string SectionName = "Sync:Retention";

    /// <summary>Operator opt-in. Default <c>false</c>.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Cron expression. Default daily 03:00 UTC (off-peak).</summary>
    public string CronExpression { get; set; } = "0 3 * * *";

    /// <summary>Batch size per <c>DELETE TOP (N)</c> loop iteration. Avoids lock escalation.</summary>
    public int DeleteBatchSize { get; set; } = 5000;

    /// <summary>
    /// Hard cap on total rows deleted per table per run. Defense against runaway
    /// cleanup on a fresh enable against a years-old backlog. Set to 0 for unbounded
    /// (only do this once you've sized the initial backlog).
    /// </summary>
    public int MaxDeletedPerTablePerRun { get; set; } = 50_000;

    /// <summary>Per-window retention for <c>sync.runs</c> (Succeeded status).</summary>
    public int SuccessfulRunsRetentionDays { get; set; } = 30;

    /// <summary>Per-window retention for <c>sync.runs</c> (Failed/DeadLettered/Cancelled).</summary>
    public int FailedRunsRetentionDays { get; set; } = 90;

    /// <summary>Per-window retention for <c>sync.failures</c>.</summary>
    public int FailureRowsRetentionDays { get; set; } = 90;

    /// <summary>Per-window retention for <c>sync.dead_letters</c>.</summary>
    public int DeadLettersRetentionDays { get; set; } = 365;

    /// <summary>
    /// Operator-declared list of outbox tables to maintain. Sync.Infrastructure cannot
    /// know about modules' tables directly (modules are independently deployable);
    /// operators register their outbox tables here so cleanup is per-deploy-explicit.
    ///
    /// <para>
    /// Each entry: <c>{ Schema, Table, RetentionDays, StatusColumn, ProcessedStatusValue }</c>.
    /// Cleanup deletes rows where <c>StatusColumn = ProcessedStatusValue AND ProcessedAt &lt; cutoff</c>.
    /// </para>
    /// </summary>
    public List<SyncOutboxRetentionTarget> OutboxTables { get; set; } = new();
}

/// <summary>
/// One outbox table the operator wants Phase 9 retention to maintain. Generic enough
/// to cover any module's outbox without Sync.Infrastructure knowing about that module.
/// </summary>
public sealed class SyncOutboxRetentionTarget
{
    /// <summary>SQL schema (e.g. <c>sync_student</c>).</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>Table name (e.g. <c>student_outbox</c>).</summary>
    public string Table { get; set; } = string.Empty;

    /// <summary>Retention window for processed rows.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Column on the table that holds the lifecycle status (e.g. <c>Status</c>).</summary>
    public string StatusColumn { get; set; } = "Status";

    /// <summary>Int value indicating "Processed". Module enums map Processed = 1.</summary>
    public int ProcessedStatusValue { get; set; } = 1;

    /// <summary>Column whose age we measure against. Default <c>ProcessedAt</c>.</summary>
    public string TimestampColumn { get; set; } = "ProcessedAt";
}