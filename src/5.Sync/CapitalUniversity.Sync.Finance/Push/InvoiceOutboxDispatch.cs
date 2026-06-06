using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Push;

public sealed class InvoiceOutboxDispatch
{
    public required InvoiceOutboxEntity Row { get; init; }
    public required ExternalInvoice Payload { get; init; }
}
