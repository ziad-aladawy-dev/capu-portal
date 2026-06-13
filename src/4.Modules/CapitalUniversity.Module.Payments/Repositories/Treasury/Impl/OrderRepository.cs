using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

public class OrderRepository : IOrderRepository
{
    private readonly CoreDbContext _context;

    public OrderRepository(CoreDbContext context) => _context = context;

    public Task<Order?> GetByIdAsync(Guid id, bool includeFees = false, bool includeTransactions = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Order>().AsQueryable();
        if (includeFees) query = query.Include(o => o.Fees);
        if (includeTransactions) query = query.Include(o => o.Transactions);
        if (includeFees && includeTransactions) query = query.AsSplitQuery();
        return query.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public Task<Order?> GetByMerchantOrderIdAsync(string merchantOrderId, bool includeFees = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Order>().AsNoTracking().AsQueryable();
        if (includeFees) query = query.Include(o => o.Fees);
        return query.FirstOrDefaultAsync(o => o.MerchantOrderId == merchantOrderId, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.StudentId == studentId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetPendingPaymentOlderThanAsync(DateTime cutoffUtc, int maxCount, CancellationToken cancellationToken = default) =>
        await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt < cutoffUtc)
            .OrderBy(o => o.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await _context.Set<Order>().AddAsync(order, cancellationToken);

    public void Update(Order order) => _context.Set<Order>().Update(order);

    public void ResetChangeTracker() => _context.ChangeTracker.Clear();
}
