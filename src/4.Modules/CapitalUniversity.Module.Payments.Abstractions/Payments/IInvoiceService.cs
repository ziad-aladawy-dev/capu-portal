

using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions;

public interface IInvoiceService
{
    Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged list with filters (status / date ranges / amount ranges /
    /// currency / studentId). Scope check applied to <see cref="InvoiceSearchQuery.StudentId"/>
    /// when present — out-of-scope returns an empty page. Cross-student queries
    /// (no <c>StudentId</c>) are admin-only by route permission.
    /// </summary>
    Task<PagedResult<InvoiceResponse>> SearchAsync(InvoiceSearchQuery query, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);

    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-cancels invoices. Each row commits independently — failures land in
    /// <see cref="BulkActionResult.Failures"/> (NotFound for missing/out-of-scope,
    /// Conflict for Paid/Refunded or record-closed invoices). Already-Cancelled
    /// rows are treated as a successful no-op so replays are safe.
    /// </summary>
    Task<BulkActionResult> BulkCancelAsync(IReadOnlyList<Guid> ids, string reason, CancellationToken cancellationToken = default);
}
