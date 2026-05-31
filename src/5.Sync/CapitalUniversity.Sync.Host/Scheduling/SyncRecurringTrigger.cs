using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using Hangfire;

namespace CapitalUniversity.Sync.Host.Scheduling;

/// <summary>
/// Thin Hangfire entry point used by every recurring sync job. Responsibility:
///   • selecting module
///   • selecting direction
///   • calling the dispatcher
/// No business logic. No enrichment beyond the trigger source.
/// </summary>
public sealed class SyncRecurringTrigger
{
    private const string TriggerSource = "scheduled";

    private readonly ISyncDispatcher _dispatcher;

    public SyncRecurringTrigger(ISyncDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [AutomaticRetry(Attempts = 0)]
    public Task TriggerAsync(string moduleName, SyncDirection direction)
    {
        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = TriggerSource
        };

        return _dispatcher.DispatchAsync(moduleName, direction, metadata, CancellationToken.None);
    }
}