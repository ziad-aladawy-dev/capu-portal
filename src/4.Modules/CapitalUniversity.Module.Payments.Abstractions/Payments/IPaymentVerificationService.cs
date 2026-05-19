using CapitalUniversity.Core.Abstractions.Payments.DTOs;

namespace CapitalUniversity.Core.Abstractions.Payments;

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
}
