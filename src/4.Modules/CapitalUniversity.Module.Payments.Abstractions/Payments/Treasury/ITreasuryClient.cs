namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Outbound client for the HU Treasury payment system. Phase 2 covers receipt
/// retrieval only; initiation / status / refund methods are added in later
/// phases.
/// </summary>
public interface ITreasuryClient
{
    /// <summary>
    /// Fetches receipts from <c>GET /api/payments/receipts</c>, returning only
    /// those whose <c>ConnectionTypeId</c> matches the configured value (6).
    /// </summary>
    Task<IReadOnlyList<TreasuryReceiptDto>> GetReceiptsAsync(CancellationToken cancellationToken = default);
}
