using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
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

    public Task<bool> IsReferencedByPlanAsync(Guid courseId, CancellationToken cancellationToken = default) =>
        _context.Set<AcademicPlanCourse>().AnyAsync(apc => apc.CourseId == courseId, cancellationToken);

    private static readonly HashSet<string> CourseSortFields = new(StringComparer.OrdinalIgnoreCase) { "code", "creditHours", "createdAt" };

    public async Task<PagedResult<Course>> SearchAsync(CourseSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Courses.AsNoTracking().AsQueryable();

        if (query.Category.HasValue) q = q.Where(c => c.Category == query.Category.Value);
        if (query.IsActive.HasValue) q = q.Where(c => c.IsActive == query.IsActive.Value);
        if (query.MinCreditHours.HasValue) q = q.Where(c => c.CreditHours >= query.MinCreditHours.Value);
        if (query.MaxCreditHours.HasValue) q = q.Where(c => c.CreditHours <= query.MaxCreditHours.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => c.Code.Contains(s) || c.Title.Contains(s));
        }

        var sort = SortClause.Parse(query.Sort, CourseSortFields);
        IOrderedQueryable<Course>? ord = null;
        if (sort.Count == 0)
        {
            ord = q.OrderBy(c => c.Code);
        }
        else
        {
            foreach (var c in sort)
            {
                ord = (c.Field.ToLowerInvariant(), c.Descending, ord) switch
                {
                    ("code", true, null) => q.OrderByDescending(x => x.Code),
                    ("code", false, null) => q.OrderBy(x => x.Code),
                    ("credithours", true, null) => q.OrderByDescending(x => x.CreditHours),
                    ("credithours", false, null) => q.OrderBy(x => x.CreditHours),
                    ("createdat", true, null) => q.OrderByDescending(x => x.CreatedAt),
                    ("createdat", false, null) => q.OrderBy(x => x.CreatedAt),
                    ("code", true, _) => ord!.ThenByDescending(x => x.Code),
                    ("code", false, _) => ord!.ThenBy(x => x.Code),
                    ("credithours", true, _) => ord!.ThenByDescending(x => x.CreditHours),
                    ("credithours", false, _) => ord!.ThenBy(x => x.CreditHours),
                    ("createdat", true, _) => ord!.ThenByDescending(x => x.CreatedAt),
                    ("createdat", false, _) => ord!.ThenBy(x => x.CreatedAt),
                    _ => ord,
                };
            }
        }

        q = ord ?? q.OrderBy(c => c.Code);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Course>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }
}
