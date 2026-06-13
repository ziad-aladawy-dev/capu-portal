using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class SemesterRepository : ISemesterRepository
{
    private readonly CoreDbContext _context;

    public SemesterRepository(CoreDbContext context)
    {
        _context = context;
    }

    public async Task<Semester?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Semester>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Semester?> GetCurrentAsync()
    {
        return await _context.Set<Semester>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsCurrent);
    }

    public async Task<IReadOnlyList<Semester>> GetByAcademicYearIdAsync(Guid academicYearId)
    {
        return await _context.Set<Semester>()
            .AsNoTracking()
            .Where(x => x.AcademicYearId == academicYearId)
            .OrderBy(x => x.Order)
            .ToListAsync();
    }

    public async Task<bool> HasOverlapAsync(Guid academicYearId, DateTime startDate, DateTime endDate, Guid? excludeId = null)
    {
        var query = _context.Set<Semester>()
            .AsNoTracking()
            .Where(x => x.AcademicYearId == academicYearId);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query.AnyAsync(x => 
            (startDate >= x.StartDate && startDate <= x.EndDate) ||
            (endDate >= x.StartDate && endDate <= x.EndDate) ||
            (startDate <= x.StartDate && endDate >= x.EndDate));
    }

    public async Task AddAsync(Semester semester)
    {
        await _context.Set<Semester>().AddAsync(semester);
    }

    public void Update(Semester semester)
    {
        _context.Set<Semester>().Update(semester);
    }

    public void Delete(Semester semester)
    {
        _context.Set<Semester>().Remove(semester);
    }
}
