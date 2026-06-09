namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Pulls receipts from HU Treasury and upserts them into the local
/// <c>TreasuryReceipts</c> cache (external-wins). Driven by a recurring job.
/// </summary>
public interface IReceiptSyncService
{
    /// <summary>Returns the number of receipt rows persisted (inserted + updated).</summary>
    Task<int> SyncAsync(CancellationToken cancellationToken = default);
}
