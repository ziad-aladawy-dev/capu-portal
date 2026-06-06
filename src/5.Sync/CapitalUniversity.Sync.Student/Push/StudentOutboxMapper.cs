using System.Text.Json;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Parses the outbox row's JSON payload into an <see cref="ExternalStudent"/>.
///
/// <para>
/// <b>Schema-drift defense.</b> Two checks run before any data leaves this mapper:
/// <list type="number">
///   <item>
///     <see cref="StudentOutboxEntity.PayloadSchemaVersion"/> must equal
///     <see cref="StudentOutboxEntity.CurrentPayloadSchemaVersion"/>. Older or newer
///     versions throw with a descriptive error — the outbox row stays Pending and the
///     pipeline records the version mismatch on <c>LastError</c>.
///   </item>
///   <item>
///     The JSON deserializer is configured with
///     <c>UnmappedMemberHandling = Disallow</c> in
///     <see cref="OutboxPayloadSerializer"/>. A field on the upstream payload
///     that the local DTO doesn't declare causes a loud parse failure rather than a
///     silent drop. Likewise, <c>required</c> fields missing from the JSON throw.
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class StudentOutboxMapper : IRecordMapper<StudentOutboxEntity, StudentOutboxDispatch>
{
    public StudentOutboxDispatch Map(StudentOutboxEntity external)
    {
        ArgumentNullException.ThrowIfNull(external);

        if (external.PayloadSchemaVersion != StudentOutboxEntity.CurrentPayloadSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Outbox payload schema version mismatch for ExternalStudentId={external.ExternalStudentId}: " +
                $"row={external.PayloadSchemaVersion} expected={StudentOutboxEntity.CurrentPayloadSchemaVersion}. " +
                "Migrate the row or extend the mapper before retrying.");
        }

        ExternalStudent payload;
        try
        {
            payload = OutboxPayloadSerializer.Deserialize<ExternalStudent>(external.Payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Outbox payload JSON invalid for ExternalStudentId={external.ExternalStudentId}: {ex.Message}",
                ex);
        }

        return new StudentOutboxDispatch
        {
            Row = external,
            Payload = payload
        };
    }
}