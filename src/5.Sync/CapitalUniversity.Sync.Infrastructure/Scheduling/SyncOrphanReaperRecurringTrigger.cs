namespace CapitalUniversity.Sync.Infrastructure.Scheduling;

/// <summary>
/// Hangfire-invoked thin wrapper around <see cref="SyncOrphanReaperService"/>.
/// Same pattern as <c>SyncRetentionRecurringTrigger</c> — keeps the deserialized
/// method-call expression deterministic.
/// </summary>
public sealed class SyncOrphanReaperRecurringTrigger
{
    private readonly SyncOrphanReaperService _service;

    public SyncOrphanReaperRecurringTrigger(SyncOrphanReaperService service)
    {
        _service = service;
    }

    public Task TriggerAsync(CancellationToken cancellationToken) =>
        _service.RunAsync(cancellationToken);
}