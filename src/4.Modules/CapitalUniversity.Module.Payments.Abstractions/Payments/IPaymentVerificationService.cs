

using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions;

/// <summary>
/// Gateway-integration seam. The implementation in Payments records the
/// reported transaction with idempotency-key dedup and reflects the result
/// onto the parent <c>Invoice.Status</c> (Pending → PartiallyPaid → Paid).
///
/// <para>
/// The provider-specific verification (signature check, callback URL allow-
/// listing, etc.) belongs to the API-layer webhook handler that calls in
/// here — this contract is intentionally provider-agnostic.
/// </para>
/// </summary>
public interface IPaymentVerificationService
{
    Task<PaymentTransactionResponse> RecordAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransactionResponse>> GetForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged transactions search across invoices. Filters: <c>invoiceId</c>,
    /// <c>studentId</c> (joins Invoice), <c>status</c>, <c>provider</c>,
    /// <c>from/to</c> dates, amount range, free-text on provider + transaction id.
    /// </summary>
    Task<PagedResult<PaymentTransactionResponse>> SearchAsync(PaymentTransactionSearchQuery query, CancellationToken cancellationToken = default);
}
