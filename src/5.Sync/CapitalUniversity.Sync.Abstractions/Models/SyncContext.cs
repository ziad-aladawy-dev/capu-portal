using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncContext
{
    public required string ModuleName { get; init; }

    public required SyncDirection Direction { get; init; }

    public required SyncRunMetadata Metadata { get; init; }

    public Guid CorrelationId => Metadata.CorrelationId;

    /// <summary>
    /// Current Hangfire-attempt number for this execution (1-based). 1 = first attempt,
    /// 2+ = retries. Used by the pipeline and modules to surface replay semantics
    /// without changing replay behavior.
    /// </summary>
    public int Attempt { get; init; } = 1;
}