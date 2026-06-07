using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, bool includeFees = false, bool includeTransactions = false, CancellationToken cancellationToken = default);
    Task<Order?> GetByMerchantOrderIdAsync(string merchantOrderId, bool includeFees = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetPendingPaymentOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    void Update(Order order);
    void ResetChangeTracker();
}
