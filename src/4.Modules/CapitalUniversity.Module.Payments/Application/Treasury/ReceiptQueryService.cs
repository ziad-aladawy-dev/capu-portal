using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Read-side over locally synced Treasury receipts. No scope check — receipts
/// are institution-wide reference data, gated by the transactions permission
/// at the controller.
/// </summary>
public sealed class ReceiptQueryService : IReceiptQueryService
{
    private readonly ITreasuryReceiptRepository _receipts;

    public ReceiptQueryService(ITreasuryReceiptRepository receipts)
    {
        _receipts = receipts;
    }

    public async Task<IReadOnlyList<ReceiptResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var receipts = await _receipts.GetAllAsync(cancellationToken);
        return receipts.Select(ToResponse).ToList();
    }

    private static ReceiptResponse ToResponse(TreasuryReceipt r) => new()
    {
        Id = r.Id,
        ExternalReceiptId = r.ExternalReceiptId,
        Name = r.Name,
        UnitAmount = r.UnitAmount,
        Currency = r.Currency,
        IsActive = r.IsActive,
        LastSyncedAt = r.ExternallySourced.LastSyncedAt,
    };
}
