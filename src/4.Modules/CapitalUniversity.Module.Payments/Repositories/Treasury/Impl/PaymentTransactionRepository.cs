using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

/// <summary>Repository for the new Treasury audit transaction (TreasuryPaymentTransactions).</summary>
public class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly CoreDbContext _context;

    public PaymentTransactionRepository(CoreDbContext context) => _context = context;

    public async Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default) =>
        await _context.Set<PaymentTransaction>().AddAsync(transaction, cancellationToken);

    public Task<bool> ExistsAsync(string merchantOrderId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.Set<PaymentTransaction>()
            .AsNoTracking()
            .AnyAsync(t => t.MerchantOrderId == merchantOrderId && t.IdempotencyKey == idempotencyKey, cancellationToken);
}
