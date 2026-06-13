using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories.Treasury;

public class StudentFeeRepository : IStudentFeeRepository
{
    private readonly CoreDbContext _context;

    public StudentFeeRepository(CoreDbContext context) => _context = context;

    public Task<StudentFee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Set<StudentFee>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StudentFee>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<StudentFee>();
        return await _context.Set<StudentFee>()
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentFee>> GetPendingForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentFee>()
            .AsNoTracking()
            .Where(f => f.StudentId == studentId && f.Status == FeeStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(StudentFee fee, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentFee>().AddAsync(fee, cancellationToken);

    public void Update(StudentFee fee) => _context.Set<StudentFee>().Update(fee);

    public void ResetChangeTracker() => _context.ChangeTracker.Clear();
}
