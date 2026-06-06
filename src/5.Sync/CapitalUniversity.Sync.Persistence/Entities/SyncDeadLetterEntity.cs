using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Persistence.Entities;

public sealed class SyncDeadLetterEntity
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string HangfireJobId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public SyncDirection Direction { get; set; }
    public int AttemptedCount { get; set; }
    public DateTimeOffset TerminalAt { get; set; }
    public string? LastError { get; set; }
}