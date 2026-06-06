using System.Diagnostics;

namespace CapitalUniversity.Sync.Infrastructure.Observability;

/// <summary>
/// Phase X.3 fix #8: centralized <see cref="ActivitySource"/> for the sync
/// platform. Wired into the BCL diagnostic infrastructure — operators add
/// OpenTelemetry / Application Insights / Datadog by registering a listener
/// against <see cref="ActivitySourceName"/>.
///
/// <para>
/// The sync layer ships zero coupling to a specific tracing backend; this is
/// just the activity-emission seam. Activities created here propagate
/// <c>CorrelationId</c>, <c>ModuleName</c>, <c>Direction</c>, and per-stage
/// timing as tags — the same data the existing structured-log line carries,
/// now also available to any distributed-tracing collector with no code change.
/// </para>
///
/// <para>
/// The legacy <see cref="Stopwatch"/> totals in <c>SyncPipeline</c> are kept
/// for backward compatibility with the existing <c>Pipeline metrics.</c> log
/// line. <see cref="ActivitySource"/> is additive — operators who don't wire
/// a listener pay near-zero cost (the BCL short-circuits unused sources).
/// </para>
/// </summary>
public static class SyncDiagnostics
{
    /// <summary>Stable name for tracing-backend configuration.</summary>
    public const string ActivitySourceName = "CapitalUniversity.Sync";

    /// <summary>Bump this when the activity-tag schema changes.</summary>
    public const string ActivitySourceVersion = "1.0.0";

    /// <summary>Shared <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, ActivitySourceVersion);

    /// <summary>Activity name for a full pipeline run.</summary>
    public const string PipelineRunActivity = "sync.pipeline.run";

    /// <summary>Activity name for a per-batch stage execution.</summary>
    public const string PipelineStageActivity = "sync.pipeline.stage";

    // Tag-name constants — keep stable so dashboards filtering on them don't break.
    public const string TagCorrelationId = "sync.correlation_id";
    public const string TagModule = "sync.module";
    public const string TagDirection = "sync.direction";
    public const string TagBatchSize = "sync.batch_size";
    public const string TagBatchIndex = "sync.batch_index";
    public const string TagStage = "sync.stage";
    public const string TagAttempt = "sync.attempt";
    public const string TagExtracted = "sync.extracted";
    public const string TagProcessed = "sync.processed";
    public const string TagValidationFailed = "sync.validation_failed";
    public const string TagWriterSkipped = "sync.writer_skipped";
    public const string TagIdempotencySkipped = "sync.idempotency_skipped";
}