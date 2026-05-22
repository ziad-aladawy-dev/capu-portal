using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;

namespace CapitalUniversity.Modules.CourseOffering.Abstractions;

/// <summary>
/// Owns the runtime availability view of a catalog course for one academic
/// term + one structure-node target. Does NOT own: registration orchestration,
/// schedule conflict logic, fee logic, transcript logic, prerequisite
/// resolution. Those live in their own modules and consume offerings by id.
/// </summary>
public interface ICourseOfferingService
{
    Task<CourseOfferingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Slim list for a given (structureNode, semester). Optional <paramref name="status"/> narrows to a single lifecycle stage. No N+1 — single query.</summary>
    Task<IReadOnlyList<CourseOfferingResponse>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>All sections of one course running in one term, across structure-node targets the caller may access.</summary>
    Task<IReadOnlyList<CourseOfferingResponse>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateCourseOfferingRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateCourseOfferingRequest request, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);

    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);
}
