using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;

namespace CapitalUniversity.Core.Abstractions.Courses;

public interface IAcademicPlanService
{
    Task<AcademicPlanResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicPlanResponse>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default);

    /// <summary>Paged plan search. Out-of-scope rows filtered post-query.</summary>
    Task<PagedResult<AcademicPlanResponse>> SearchAsync(AcademicPlanSearchQuery query, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAcademicPlanRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAcademicPlanRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> AddCourseAsync(Guid planId, AddPlanCourseRequest request, CancellationToken cancellationToken = default);
    Task RemoveCourseAsync(Guid planId, Guid planCourseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply an atomic diff to the plan's course composition. All additions and
    /// removals commit together in one transaction; if any single step is
    /// invalid (course missing, entry not on this plan, duplicate add, mutability
    /// violation), the whole batch is rejected. The plan never lands in a
    /// half-applied state.
    /// </summary>
    Task BatchUpdateCoursesAsync(Guid planId, BatchPlanCoursesRequest request, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>3.9 — bulk delete academic plans. Per-row commits.</summary>
    Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}
