using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxEntity
{
    public const int MaxAttempts = 5;
    public const int CurrentPayloadSchemaVersion = 1;

    public Guid Id { get; set; }
    public string ExternalScheduleSlotId { get; set; } = string.Empty;
    public OutboxOperation Operation { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int PayloadSchemaVersion { get; set; } = CurrentPayloadSchemaVersion;
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
