using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncRunRecord
{
    public required Guid CorrelationId { get; init; }

    public required string ModuleName { get; init; }

    public required SyncDirection Direction { get; init; }

    public required string TriggeredBy { get; init; }

    public required string Queue { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    public SyncRunStatus Status { get; init; } = SyncRunStatus.Enqueued;
}