using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Finance.Push;

public sealed class InvoiceOutboxValidator : IRecordValidator<InvoiceOutboxDispatch>
{
    public bool IsValid(InvoiceOutboxDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Payload.ExternalInvoiceId))
        {
            error = "Outbox payload missing ExternalInvoiceId.";
            return false;
        }

        if (!string.Equals(record.Row.ExternalInvoiceId, record.Payload.ExternalInvoiceId, StringComparison.Ordinal))
        {
            error = "Outbox payload ExternalInvoiceId mismatch.";
            return false;
        }

        error = null;
        return true;
    }
}
