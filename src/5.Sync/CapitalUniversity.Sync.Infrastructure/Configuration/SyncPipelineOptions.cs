namespace CapitalUniversity.Sync.Infrastructure.Configuration;

/// <summary>
/// Pipeline-wide tuning knobs (Phase 8). Applies to every module's runs uniformly
/// — modules continue to own per-domain batch sizes via their own option types.
/// </summary>
public sealed class SyncPipelineOptions
{
    /// <summary>
    /// Number of times the pipeline retries the WRITER step within a single batch
    /// before propagating the failure to <c>SyncResult.Failed</c> (which then engages
    /// Hangfire's whole-execution retry policy). 0 = current Phase 5/6/7 behavior:
    /// a single writer attempt; failure fails the pipeline and the next Hangfire
    /// retry re-extracts + re-processes ALL prior batches.
    ///
    /// <para>
    /// Per-batch retry only wraps the writer (the most common transient-failure
    /// surface — DB hiccup, deadlock, network timeout). Mapper and validator
    /// failures still fail the pipeline immediately because re-running them on the
    /// same input would produce the same result; only the writer's side effects
    /// can be transient.
    /// </para>
    ///
    /// <para>
    /// <b>Retry-layer coordination (Phase X.4 documentation).</b> Several modules'
    /// writers carry their own internal retry loops — e.g. <c>StudentWriter</c>
    /// retries once on a unique-constraint race (Phase 5 hardening). When this
    /// option is &gt; 0, the pipeline adds an OUTER retry layer on top of that
    /// inner one, so a single batch can experience
    /// <c>(1 + PerBatchWriterRetryAttempts) × (writer's internal attempts)</c>
    /// total tries.
    /// </para>
    /// <para>
    /// The default value of <b>0</b> keeps the layers decoupled — only the writer's
    /// internal retry runs, exactly matching the Phase 5/6/7 behavior with no
    /// double-counting. Operators enabling pipeline-layer retry should size it
    /// against their writer's known internal retry count to avoid retry
    /// amplification. For the outbox push writers (Phase 6), which have NO
    /// internal retry but rely on per-row catch + outbox-status persistence,
    /// raising this option to 1–2 is the recommended way to absorb transient
    /// DB-write hiccups without engaging Hangfire's 60/300/900/3600-second
    /// whole-execution backoff.
    /// </para>
    /// </summary>
    public int PerBatchWriterRetryAttempts { get; set; } = 0;

    /// <summary>
    /// Backoff between per-batch writer retries. Pure synchronous delay inside the
    /// pipeline run — no Hangfire interaction. 0 = retry immediately. Default 1s.
    /// </summary>
    public TimeSpan PerBatchWriterRetryBackoff { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Phase X.3 fix #1: strict-visibility mode for writers that swallow per-row
    /// failures (the Phase 6 outbox push pattern returns a lower processed count
    /// rather than throwing — preserving per-row isolation). When this flag is
    /// <c>false</c> (default), the pipeline still reports <c>RecordsFailed</c>
    /// accurately in <see cref="SyncResult"/> + the metrics log but keeps
    /// <c>Success = true</c> so Hangfire's <c>[AutomaticRetry]</c> doesn't
    /// engage for transient sink failures (the next recurring tick re-attempts
    /// via the outbox's <c>Pending</c> rows). When <c>true</c>, any run with
    /// <c>WriterSkipped &gt; 0</c> returns <c>SyncResult.Failed</c> — surfacing
    /// the failure to the dashboard at the cost of more aggressive retries.
    ///
    /// <para>
    /// Phase X.4 added an adaptive default policy: zero-progress runs
    /// (<c>WriterSkipped &gt; 0 AND Processed == 0</c>) fail unconditionally,
    /// regardless of this flag, since they're the "upstream is dead" signature.
    /// This flag's job is the stricter "any skip = fail" mode for partial-progress
    /// runs too.
    /// </para>
    /// </summary>
    public bool FailRunOnAnyWriterSkip { get; set; } = false;

    /// <summary>
    /// Phase X.5 hard cap on the in-run <c>IdempotencyHandler</c>'s <c>HashSet&lt;TKey&gt;</c>.
    /// When the dedup set's size reaches this value, the pipeline returns
    /// <c>SyncResult.Failed</c> with a descriptive error rather than continuing
    /// to grow memory indefinitely.
    ///
    /// <para>
    /// Each entry is a ~64-byte string for typical external IDs, so the default
    /// of <b>10,000,000</b> caps memory at ~640 MB worst-case. The existing
    /// <see cref="SyncPipeline.IdempotencyMemoryWarningThreshold"/> emits a
    /// single Warning at 1 M keys (operator hint that they're approaching the
    /// cap); this option is the hard fail-fast that prevents an OOM-killed worker.
    /// </para>
    /// <para>
    /// Set to 0 to disable the cap (legacy behavior — silent growth until OOM).
    /// Operators should ONLY do this if they've sized the worker's memory
    /// envelope against the expected dedup-set size for their largest cursor
    /// window.
    /// </para>
    /// </summary>
    public int MaxIdempotencyKeysPerRun { get; set; } = 10_000_000;
}