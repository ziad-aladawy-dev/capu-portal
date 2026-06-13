using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

public class ServiceReceiptMappingRepository : IServiceReceiptMappingRepository
{
    private readonly CoreDbContext _context;

    public ServiceReceiptMappingRepository(CoreDbContext context) => _context = context;

    public Task<ServiceReceiptMapping?> GetActiveByServiceAsync(Guid studentServiceId, CancellationToken cancellationToken = default) =>
        _context.Set<ServiceReceiptMapping>()
            .AsNoTracking()
            .Include(m => m.Receipt)
            .FirstOrDefaultAsync(m => m.StudentServiceId == studentServiceId && m.IsActive, cancellationToken);

    public Task<ServiceReceiptMapping?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<ServiceReceiptMapping>()
            .AsNoTracking()
            .Include(m => m.Receipt)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ServiceReceiptMapping>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<ServiceReceiptMapping>()
            .AsNoTracking()
            .Include(m => m.Receipt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ServiceReceiptMapping mapping, CancellationToken cancellationToken = default) =>
        await _context.Set<ServiceReceiptMapping>().AddAsync(mapping, cancellationToken);

    public void Update(ServiceReceiptMapping mapping) => _context.Set<ServiceReceiptMapping>().Update(mapping);
}
