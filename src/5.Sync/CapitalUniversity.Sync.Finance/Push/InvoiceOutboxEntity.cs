using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Finance.Push;

/// <summary>
/// Outbox row for Internal → External Invoice push. Same shape as the other
/// modules' outboxes — every push-enabled domain owns its own copy.
/// </summary>
public sealed class InvoiceOutboxEntity
{
    public const int MaxAttempts = 5;

    public const int CurrentPayloadSchemaVersion = 1;

    public Guid Id { get; set; }
    public string ExternalInvoiceId { get; set; } = string.Empty;
    public OutboxOperation Operation { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int PayloadSchemaVersion { get; set; } = CurrentPayloadSchemaVersion;
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
