
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class AcademicYearRepository : IAcademicYearRepository
{
    private readonly CoreDbContext _context;

    public AcademicYearRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<AcademicYear?> GetByIdAsync(Guid id)
    {
        return await _context.Set<AcademicYear>()
            .Include(x => x.Semesters)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<AcademicYear?> GetCurrentAsync()
    {
        return await _context.Set<AcademicYear>()
            .FirstOrDefaultAsync(x => x.IsCurrent);
    }

    public async Task<IEnumerable<AcademicYear>> GetAllAsync()
    {
        return await _context.Set<AcademicYear>()
            .OrderByDescending(x => x.StartDate)
            .ToListAsync();
    }

    public async Task<bool> HasOverlapAsync(DateTime startDate, DateTime endDate, Guid? excludeId = null)
    {
        var query = _context.Set<AcademicYear>().AsQueryable();

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(x => 
            (startDate >= x.StartDate && startDate <= x.EndDate) ||
            (endDate >= x.StartDate && endDate <= x.EndDate) ||
            (startDate <= x.StartDate && endDate >= x.EndDate));
    }

    public async Task AddAsync(AcademicYear academicYear)
    {
        await _context.Set<AcademicYear>().AddAsync(academicYear);
    }

    public void Update(AcademicYear academicYear)
    {
        _context.Set<AcademicYear>().Update(academicYear);
    }

    public void Delete(AcademicYear academicYear)
    {
        _context.Set<AcademicYear>().Remove(academicYear);
    }
}
