using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Pull;

/// <summary>
/// Maps an upstream <see cref="ExternalInvoice"/> into a Core
/// <see cref="Invoice"/> wrapped in <see cref="InvoiceSyncDispatch"/>. The
/// wrapper retains the upstream student key so the writer can resolve it via
/// the gateway before the gateway persists.
/// </summary>
public sealed class InvoiceMapper : IRecordMapper<ExternalInvoice, InvoiceSyncDispatch>
{
    public InvoiceSyncDispatch Map(ExternalInvoice external)
    {
        ArgumentNullException.ThrowIfNull(external);

        var entity = new Invoice
        {
            ExternallySourced = new()
            {
                ExternalId = external.ExternalInvoiceId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            // StudentId set by the writer once ICoreWriteGateway.ResolveIdByExternalIdAsync
            // returns the Core student's Guid. Empty here is a sentinel the writer enforces.
            StudentId = Guid.Empty,

            Status = external.Status,
            TotalAmount = external.TotalAmount,
            Currency = external.Currency.Trim().ToUpperInvariant(),
            DueAt = external.DueAt,
        };

        return new InvoiceSyncDispatch
        {
            Entity = entity,
            ExternalStudentId = external.ExternalStudentId.Trim()
        };
    }
}
