using System.Text.Json;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxMapper : IRecordMapper<ScheduleSlotOutboxEntity, ScheduleSlotOutboxDispatch>
{
    public ScheduleSlotOutboxDispatch Map(ScheduleSlotOutboxEntity external)
    {
        ArgumentNullException.ThrowIfNull(external);

        if (external.PayloadSchemaVersion != ScheduleSlotOutboxEntity.CurrentPayloadSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Outbox payload schema version mismatch for ExternalScheduleSlotId={external.ExternalScheduleSlotId}: " +
                $"row={external.PayloadSchemaVersion} expected={ScheduleSlotOutboxEntity.CurrentPayloadSchemaVersion}.");
        }

        ExternalScheduleSlot payload;
        try
        {
            payload = OutboxPayloadSerializer.Deserialize<ExternalScheduleSlot>(external.Payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Outbox payload JSON invalid for ExternalScheduleSlotId={external.ExternalScheduleSlotId}: {ex.Message}",
                ex);
        }

        return new ScheduleSlotOutboxDispatch
        {
            Row = external,
            Payload = payload
        };
    }
}
