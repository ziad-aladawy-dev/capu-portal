using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

public interface ITreasuryReceiptRepository
{
    Task<TreasuryReceipt?> GetByExternalIdAsync(string externalReceiptId, CancellationToken cancellationToken = default);
    Task<TreasuryReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TreasuryReceipt>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TreasuryReceipt receipt, CancellationToken cancellationToken = default);
    void Update(TreasuryReceipt receipt);
}
