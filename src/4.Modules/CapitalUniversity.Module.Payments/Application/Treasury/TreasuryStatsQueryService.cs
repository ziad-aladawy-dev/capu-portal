using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Institution-wide treasury aggregates. No scope check — like receipts, this
/// is admin-level reference data gated by the transactions permission at the
/// controller. Queries the context directly because the per-entity repositories
/// only expose row-level reads.
/// </summary>
public sealed class TreasuryStatsQueryService : ITreasuryStatsQueryService
{
    private readonly CoreDbContext _context;

    public TreasuryStatsQueryService(CoreDbContext context) => _context = context;

    public async Task<TreasuryStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var feesByStatus = await _context.Set<StudentFee>()
            .AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new TreasuryStatusSlice
            {
                Status = (int)g.Key,
                Count = g.Count(),
                Amount = g.Sum(f => f.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var ordersByStatus = await _context.Set<Order>()
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new TreasuryStatusSlice
            {
                Status = (int)g.Key,
                Count = g.Count(),
                Amount = g.Sum(o => o.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var monthlyFloor = DateTime.UtcNow.Date.AddMonths(-11);
        monthlyFloor = new DateTime(monthlyFloor.Year, monthlyFloor.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthly = await _context.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.PaidAt >= monthlyFloor)
            .GroupBy(p => new { p.PaidAt.Year, p.PaidAt.Month })
            .Select(g => new TreasuryMonthlyPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Amount = g.Sum(p => p.Amount),
                Count = g.Count(),
            })
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToListAsync(cancellationToken);

        decimal AmountFor(FeeStatus s) => feesByStatus.FirstOrDefault(x => x.Status == (int)s)?.Amount ?? 0m;

        return new TreasuryStatsResponse
        {
            TotalCollected = AmountFor(FeeStatus.Paid),
            OutstandingAmount = AmountFor(FeeStatus.Pending) + AmountFor(FeeStatus.IncludedInOrder),
            RefundedAmount = AmountFor(FeeStatus.Refunded),
            TotalFees = feesByStatus.Sum(x => x.Count),
            TotalOrders = ordersByStatus.Sum(x => x.Count),
            FeesByStatus = feesByStatus,
            OrdersByStatus = ordersByStatus,
            MonthlyCollected = monthly,
        };
    }
}
