using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

public class TreasuryReceiptRepository : ITreasuryReceiptRepository
{
    private readonly CoreDbContext _context;

    public TreasuryReceiptRepository(CoreDbContext context) => _context = context;

    public Task<TreasuryReceipt?> GetByExternalIdAsync(string externalReceiptId, CancellationToken cancellationToken = default) =>
        _context.Set<TreasuryReceipt>()
            .FirstOrDefaultAsync(r => r.ExternalReceiptId == externalReceiptId, cancellationToken);

    public Task<TreasuryReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<TreasuryReceipt>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TreasuryReceipt>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<TreasuryReceipt>()
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(TreasuryReceipt receipt, CancellationToken cancellationToken = default) =>
        await _context.Set<TreasuryReceipt>().AddAsync(receipt, cancellationToken);

    public void Update(TreasuryReceipt receipt) => _context.Set<TreasuryReceipt>().Update(receipt);
}
