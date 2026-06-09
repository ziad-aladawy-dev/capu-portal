using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Outbound client for the HU Treasury payment system. Speaks the per-gateway
/// wire contracts internally and exposes normalized results to the Portal.
/// </summary>
public interface ITreasuryClient
{
    /// <summary>
    /// Fetches receipts via <c>GET /api/payments/receipts?connectionTypeIds=...</c>
    /// (unwrapping the Treasury BaseResponse) for the configured connection type.
    /// </summary>
    Task<IReadOnlyList<TreasuryReceiptDto>> GetReceiptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a payment session via the gateway-specific initiate endpoint and
    /// returns the normalized MerchantOrderId + redirect URL.
    /// </summary>
    Task<TreasuryInitiateResult> InitiateAsync(Gateway gateway, TreasuryInitiateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries payment status via the gateway-specific status endpoint, returning
    /// the normalized status + settled amount.
    /// </summary>
    Task<TreasuryStatusResult> GetStatusAsync(Gateway gateway, string merchantOrderId, CancellationToken cancellationToken = default);
}
