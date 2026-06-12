using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class AcademicPlanRepository : IAcademicPlanRepository
{
    private readonly CoreDbContext _context;

    public AcademicPlanRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<AcademicPlan?> GetByIdAsync(Guid id, bool includeCourses = true, CancellationToken cancellationToken = default)
    {
        var query = _context.AcademicPlans.AsQueryable();
        if (includeCourses) query = query.Include(p => p.PlanCourses);
        return query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AcademicPlan>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default) =>
        await _context.AcademicPlans
            .AsNoTracking()
            .Where(p => p.StructureNodeId == structureNodeId)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(AcademicPlan plan, CancellationToken cancellationToken = default) =>
        await _context.AcademicPlans.AddAsync(plan, cancellationToken);

    public void Update(AcademicPlan plan) => _context.AcademicPlans.Update(plan);

    public void Delete(AcademicPlan plan) => _context.AcademicPlans.Remove(plan);

    public Task<bool> ContainsCourseAsync(Guid planId, Guid courseId, CancellationToken cancellationToken = default) =>
        _context.AcademicPlanCourses
            .AsNoTracking()
            .AnyAsync(pc => pc.AcademicPlanId == planId && pc.CourseId == courseId, cancellationToken);

    public Task<AcademicPlanCourse?> GetPlanCourseAsync(Guid planCourseId, CancellationToken cancellationToken = default) =>
        _context.AcademicPlanCourses.FirstOrDefaultAsync(pc => pc.Id == planCourseId, cancellationToken);

    public void AddPlanCourse(AcademicPlanCourse planCourse) =>
        _context.AcademicPlanCourses.Add(planCourse);

    public void RemovePlanCourse(AcademicPlanCourse planCourse) =>
        _context.AcademicPlanCourses.Remove(planCourse);

    private static readonly HashSet<string> PlanSortFields = new(StringComparer.OrdinalIgnoreCase) { "name", "effectiveFrom", "createdAt" };

    public async Task<PagedResult<AcademicPlan>> SearchAsync(AcademicPlanSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.AcademicPlans.AsNoTracking().AsQueryable();

        if (query.StructureNodeId.HasValue) q = q.Where(p => p.StructureNodeId == query.StructureNodeId.Value);
        if (query.IsActive.HasValue) q = q.Where(p => p.IsActive == query.IsActive.Value);
        if (query.EffectiveFromInclusive.HasValue) q = q.Where(p => p.EffectiveFrom >= query.EffectiveFromInclusive.Value);
        if (query.EffectiveToExclusive.HasValue) q = q.Where(p => p.EffectiveFrom < query.EffectiveToExclusive.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(s));
        }

        var sort = SortClause.Parse(query.Sort, PlanSortFields);
        IOrderedQueryable<AcademicPlan>? ord = null;
        if (sort.Count == 0)
        {
            ord = q.OrderByDescending(p => p.EffectiveFrom);
        }
        else
        {
            foreach (var c in sort)
            {
                ord = (c.Field.ToLowerInvariant(), c.Descending, ord) switch
                {
                    ("name", true, null) => q.OrderByDescending(p => p.Name),
                    ("name", false, null) => q.OrderBy(p => p.Name),
                    ("effectivefrom", true, null) => q.OrderByDescending(p => p.EffectiveFrom),
                    ("effectivefrom", false, null) => q.OrderBy(p => p.EffectiveFrom),
                    ("createdat", true, null) => q.OrderByDescending(p => p.CreatedAt),
                    ("createdat", false, null) => q.OrderBy(p => p.CreatedAt),
                    ("name", true, _) => ord!.ThenByDescending(p => p.Name),
                    ("name", false, _) => ord!.ThenBy(p => p.Name),
                    ("effectivefrom", true, _) => ord!.ThenByDescending(p => p.EffectiveFrom),
                    ("effectivefrom", false, _) => ord!.ThenBy(p => p.EffectiveFrom),
                    ("createdat", true, _) => ord!.ThenByDescending(p => p.CreatedAt),
                    ("createdat", false, _) => ord!.ThenBy(p => p.CreatedAt),
                    _ => ord,
                };
            }
        }

        q = ord ?? q.OrderByDescending(p => p.EffectiveFrom);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AcademicPlan>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }
}
