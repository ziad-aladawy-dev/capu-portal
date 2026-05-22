using CapitalUniversity.Core.Abstractions.Courses.DTOs;

namespace CapitalUniversity.Core.Abstractions.Courses;

public interface IAcademicPlanService
{
    Task<AcademicPlanResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AcademicPlanResponse>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAcademicPlanRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAcademicPlanRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> AddCourseAsync(Guid planId, AddPlanCourseRequest request, CancellationToken cancellationToken = default);
    Task RemoveCourseAsync(Guid planId, Guid planCourseId, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);
}
