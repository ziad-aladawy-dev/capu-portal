using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

public class PaymentRepository : IPaymentRepository
{
    private readonly CoreDbContext _context;

    public PaymentRepository(CoreDbContext context) => _context = context;

    public Task<Payment?> GetByFeeIdAsync(Guid feeId, CancellationToken cancellationToken = default) =>
        _context.Set<Payment>().AsNoTracking().FirstOrDefaultAsync(p => p.FeeId == feeId, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default) =>
        await _context.Set<Payment>().AddAsync(payment, cancellationToken);
}
