using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly CoreDbContext _context;

    public CourseRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Course?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Courses.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Course>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Courses.AsNoTracking().Where(c => c.Code == code);
        if (excludeId.HasValue) query = query.Where(c => c.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Course course, CancellationToken cancellationToken = default) =>
        await _context.Courses.AddAsync(course, cancellationToken);

    public void Update(Course course) => _context.Courses.Update(course);

    public void Delete(Course course) => _context.Courses.Remove(course);
}
