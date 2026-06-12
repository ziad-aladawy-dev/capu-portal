using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IAcademicPlanRepository
{
    Task<AcademicPlan?> GetByIdAsync(Guid id, bool includeCourses = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicPlan>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default);
    Task AddAsync(AcademicPlan plan, CancellationToken cancellationToken = default);
    void Update(AcademicPlan plan);
    void Delete(AcademicPlan plan);
    Task<bool> ContainsCourseAsync(Guid planId, Guid courseId, CancellationToken cancellationToken = default);
    Task<AcademicPlanCourse?> GetPlanCourseAsync(Guid planCourseId, CancellationToken cancellationToken = default);
    void AddPlanCourse(AcademicPlanCourse planCourse);
    void RemovePlanCourse(AcademicPlanCourse planCourse);

    /// <summary>Paged academic-plan search; never eager-loads composition.</summary>
    Task<PagedResult<AcademicPlan>> SearchAsync(AcademicPlanSearchQuery query, CancellationToken cancellationToken = default);
}
