using System.Text.Json;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Push;

public sealed class InvoiceOutboxMapper : IRecordMapper<InvoiceOutboxEntity, InvoiceOutboxDispatch>
{
    public InvoiceOutboxDispatch Map(InvoiceOutboxEntity external)
    {
        ArgumentNullException.ThrowIfNull(external);

        if (external.PayloadSchemaVersion != InvoiceOutboxEntity.CurrentPayloadSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Outbox payload schema version mismatch for ExternalInvoiceId={external.ExternalInvoiceId}: " +
                $"row={external.PayloadSchemaVersion} expected={InvoiceOutboxEntity.CurrentPayloadSchemaVersion}.");
        }

        ExternalInvoice payload;
        try
        {
            payload = OutboxPayloadSerializer.Deserialize<ExternalInvoice>(external.Payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Outbox payload JSON invalid for ExternalInvoiceId={external.ExternalInvoiceId}: {ex.Message}",
                ex);
        }

        return new InvoiceOutboxDispatch
        {
            Row = external,
            Payload = payload
        };
    }
}
