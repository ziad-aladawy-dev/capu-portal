using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Persistence.Entities;

public sealed class SyncRunEntity
{
    public Guid CorrelationId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public SyncDirection Direction { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string Queue { get; set; } = string.Empty;
    public SyncRunStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? HangfireJobId { get; set; }
    public DateTimeOffset EnqueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? RecordsProcessed { get; set; }
    public int? RecordsFailed { get; set; }
    public long? DurationTicks { get; set; }
    public string? LastError { get; set; }
}