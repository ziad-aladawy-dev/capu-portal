using CapitalUniversity.Core.Abstractions.Repositories;
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

    public void RemovePlanCourse(AcademicPlanCourse planCourse) =>
        _context.AcademicPlanCourses.Remove(planCourse);
}
