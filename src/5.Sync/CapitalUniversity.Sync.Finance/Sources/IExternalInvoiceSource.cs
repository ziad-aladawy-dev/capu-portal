using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Sources;

public interface IExternalInvoiceSource
{
    IAsyncEnumerable<ExternalInvoice> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        CancellationToken cancellationToken);
}
