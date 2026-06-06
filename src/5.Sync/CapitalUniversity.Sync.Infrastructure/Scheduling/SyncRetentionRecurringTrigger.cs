namespace CapitalUniversity.Sync.Infrastructure.Scheduling;

/// <summary>
/// Hangfire-invoked thin wrapper. Kept separate from <see cref="SyncRetentionService"/>
/// so Hangfire serializes/deserializes a deterministic method-call expression while
/// the service holds the heavy DI dependencies.
/// </summary>
public sealed class SyncRetentionRecurringTrigger
{
    private readonly SyncRetentionService _service;

    public SyncRetentionRecurringTrigger(SyncRetentionService service)
    {
        _service = service;
    }

    public Task TriggerAsync(CancellationToken cancellationToken) =>
        _service.RunAsync(cancellationToken);
}