using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Outbound client for the HU Treasury payment system. Status / refund methods
/// are added in later phases.
/// </summary>
public interface ITreasuryClient
{
    /// <summary>
    /// Fetches receipts from <c>GET /api/payments/receipts</c>, returning only
    /// those whose <c>ConnectionTypeId</c> matches the configured value (6).
    /// </summary>
    Task<IReadOnlyList<TreasuryReceiptDto>> GetReceiptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a payment session via the gateway-specific initiate endpoint
    /// (<c>POST /api/payments/{gateway}/initiate</c>) and returns the
    /// MerchantOrderId + redirect URL.
    /// </summary>
    Task<TreasuryInitiateResponse> InitiateAsync(Gateway gateway, TreasuryInitiateRequest request, CancellationToken cancellationToken = default);
}
