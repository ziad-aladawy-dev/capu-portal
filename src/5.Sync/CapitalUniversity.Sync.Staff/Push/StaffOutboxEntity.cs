using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Staff.Push;

/// <summary>
/// Outbox row for Internal → External Staff push. Same shape as the Students
/// module's outbox — every push-enabled domain owns its own copy so the table
/// schemas can diverge per domain (e.g. add domain-specific routing hints later)
/// without coupling modules together. Shared status/operation enums come from
/// <see cref="OutboxStatus"/> / <see cref="OutboxOperation"/>.
/// </summary>
public sealed class StaffOutboxEntity
{
    public const int MaxAttempts = 5;

    /// <summary>
    /// Schema version the outbox writer expected when serializing <see cref="Payload"/>.
    /// Push mapper enforces <c>row.PayloadSchemaVersion == CurrentPayloadSchemaVersion</c>.
    /// Mismatches fail loud rather than silently truncating the push.
    /// </summary>
    public const int CurrentPayloadSchemaVersion = 1;

    public Guid Id { get; set; }
    public string ExternalStaffId { get; set; } = string.Empty;
    public OutboxOperation Operation { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int PayloadSchemaVersion { get; set; } = CurrentPayloadSchemaVersion;
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}