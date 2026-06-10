using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Read-side for locally synced Treasury receipts. Backs the admin billing UI:
/// receipt-name resolution for fee rows and Guid receipt selection when
/// creating service → receipt mappings.
/// </summary>
public interface IReceiptQueryService
{
    /// <summary>All synced receipts (active and inactive), ordered by name.</summary>
    Task<IReadOnlyList<ReceiptResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
