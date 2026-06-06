namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Domain-only metadata that travels with a sync run. Strictly NOT a job model:
/// scheduling, retry, and execution state are owned by Hangfire.
///
/// Identity hierarchy:
///   • CorrelationId — system trace identity (ours; survives Hangfire serialization)
///   • Hangfire JobId — transport identifier owned by Hangfire
/// A "run" is identified by its CorrelationId; no separate RunId is needed.
/// </summary>
public sealed class SyncRunMetadata
{
    public required Guid CorrelationId { get; init; }

    public required string TriggeredBy { get; init; }

    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}