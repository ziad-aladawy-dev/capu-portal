using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

/// <summary>Repository for the new Treasury audit transaction (TreasuryPaymentTransactions table).</summary>
public interface IPaymentTransactionRepository
{
    Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string merchantOrderId, string idempotencyKey, CancellationToken cancellationToken = default);
}
