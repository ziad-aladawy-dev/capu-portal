using System.Text.Json;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Push;

public sealed class StaffOutboxMapper : IRecordMapper<StaffOutboxEntity, StaffOutboxDispatch>
{
    public StaffOutboxDispatch Map(StaffOutboxEntity external)
    {
        ArgumentNullException.ThrowIfNull(external);

        if (external.PayloadSchemaVersion != StaffOutboxEntity.CurrentPayloadSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Outbox payload schema version mismatch for ExternalStaffId={external.ExternalStaffId}: " +
                $"row={external.PayloadSchemaVersion} expected={StaffOutboxEntity.CurrentPayloadSchemaVersion}. " +
                "Migrate the row or extend the mapper before retrying.");
        }

        ExternalStaff payload;
        try
        {
            payload = OutboxPayloadSerializer.Deserialize<ExternalStaff>(external.Payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Outbox payload JSON invalid for ExternalStaffId={external.ExternalStaffId}: {ex.Message}",
                ex);
        }

        return new StaffOutboxDispatch
        {
            Row = external,
            Payload = payload
        };
    }
}