namespace CapitalUniversity.Sync.Student.Configuration;

public sealed class StudentSyncOptions
{
    public const string SectionName = "Sync:Student";

    /// <summary>
    /// Connection string for the Student sync DbContext. May be the same physical
    /// SQL Server as Hangfire/Sync persistence — the module's schema (sync_student)
    /// keeps its tables isolated.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Per-batch size for the Push pipeline. Reads outbox rows in chunks of this size
    /// from the in-memory page (capped by <c>StudentOutboxExtractor.MaxPerRun</c>).
    /// Validated against the same <c>MaxBatchSize</c> ceiling as <see cref="BatchSize"/>.
    /// </summary>
    public int PushBatchSize { get; set; } = 25;

    /// <summary>
    /// Pull-extractor safety-buffer ("clawback"): the extractor filters at
    /// <c>checkpoint.Cursor - <see cref="ExtractorSafetyBufferSeconds"/></c>, not at
    /// the raw cursor. Catches upstream records whose <c>UpdatedAt</c> stamp is
    /// slightly older than the cursor due to clock drift, retroactive edits, or
    /// commit-vs-stamp ordering. Default of 1 second is safe for systems with
    /// sub-second timestamp precision; operators should raise this for upstreams
    /// that perform "deep" back-dating (e.g. admin tools that set
    /// <c>UpdatedAt = now - 10 minutes</c>). The writer's external-key upsert makes
    /// the resulting tiny replay idempotent.
    /// </summary>
    public int ExtractorSafetyBufferSeconds { get; set; } = 1;
}