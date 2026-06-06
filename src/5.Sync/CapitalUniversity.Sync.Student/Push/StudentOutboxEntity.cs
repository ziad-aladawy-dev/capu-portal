using CapitalUniversity.Sync.Abstractions.Enums;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Outbox row for the Internal → External push flow. Written transactionally next to
/// the internal mutation that caused it (Phase 6 baseline: written by an admin seed
/// endpoint until a real internal write API exists). The push extractor reads
/// <see cref="OutboxStatus.Pending"/> rows in cursor order and the writer marks each
/// <see cref="OutboxStatus.Processed"/> after the external sink accepts the payload.
///
/// <para>
/// The merge key for the external system is <see cref="ExternalStudentId"/>, mirroring
/// the inbound (Pull) flow's stable identity. <see cref="Payload"/> stores a JSON
/// serialization of <c>ExternalStudent</c> so the push pipeline can replay an exact
/// snapshot of the intended state — the row is the source of truth for what should
/// have been sent, even after the internal entity drifts.
/// </para>
/// </summary>
public sealed class StudentOutboxEntity
{
    /// <summary>
    /// Poison-row cap enforced by <see cref="StudentOutboxWriter"/>.
    /// Once <see cref="AttemptCount"/> reaches this value the row is moved to
    /// <see cref="OutboxStatus.Failed"/> and stops being re-picked-up by the push tick.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Schema version the outbox writer expected when serializing <see cref="Payload"/>.
    /// Push mapper enforces <c>row.PayloadSchemaVersion == CurrentPayloadSchemaVersion</c>;
    /// mismatches fail loud (outbox row stays Pending with descriptive LastError) rather
    /// than silently truncating the push. Bump <c>CurrentPayloadSchemaVersion</c> only
    /// alongside a backward-compatible upgrade path in the mapper.
    /// </summary>
    public const int CurrentPayloadSchemaVersion = 1;

    public Guid Id { get; set; }
    public string ExternalStudentId { get; set; } = string.Empty;
    public OutboxOperation Operation { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int PayloadSchemaVersion { get; set; } = CurrentPayloadSchemaVersion;
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}