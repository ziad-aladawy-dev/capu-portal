namespace CapitalUniversity.Sync.Infrastructure.Alerting;

/// <summary>
/// Phase 10 alerting extension point. Single seam where operators wire Slack /
/// PagerDuty / email / webhook integrations without modifying the sync layer.
/// Default implementation in this assembly is <see cref="LoggingSyncAlertingHook"/>
/// — JSON-structured-log only. Operators register their own implementation in DI
/// to ship to a real channel.
///
/// <para>
/// <b>Fire-and-forget contract.</b> Implementations MUST NOT throw — exceptions
/// are caught and logged at the call site so a flaky alerting destination cannot
/// affect the sync pipeline. Implementations SHOULD complete within ~1s; longer
/// work should be queued internally.
/// </para>
/// </summary>
public interface ISyncAlertingHook
{
    Task DeadLetterAsync(SyncAlert alert, CancellationToken cancellationToken);
    Task PipelineFailureAsync(SyncAlert alert, CancellationToken cancellationToken);
}

/// <summary>
/// Generic alert payload. Carries the runtime identity (CorrelationId, ModuleName)
/// + the descriptive fields a destination might want (Title, Severity, Detail).
/// Stays JSON-friendly so a webhook implementation can serialize directly.
/// </summary>
public sealed class SyncAlert
{
    public required Guid CorrelationId { get; init; }
    public required string ModuleName { get; init; }
    public required string Direction { get; init; }
    public required string Title { get; init; }
    public required string Severity { get; init; }
    public string? Detail { get; init; }
    public string? HangfireJobId { get; init; }
    public int? AttemptCount { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}