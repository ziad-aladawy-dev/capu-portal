using CapitalUniversity.Core.Abstractions.Courses.DTOs;

namespace CapitalUniversity.Core.Abstractions.Courses;

/// <summary>
/// Catalog prerequisite graph. The service keeps the graph a DAG: every
/// mutation re-validates that no path leads from a proposed prerequisite back
/// to the dependent course.
/// </summary>
public interface ICoursePrerequisiteService
{
    /// <summary>Enriched prerequisite list for one course (code/title/credits joined from the catalog).</summary>
    Task<IReadOnlyList<CoursePrerequisiteResponse>> GetForCourseAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Every edge in the graph — lets clients run cycle checks and health cross-references locally.</summary>
    Task<IReadOnlyList<CoursePrerequisiteLinkResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Batch replace: the request body becomes the course's exact prerequisite set.</summary>
    Task SetAsync(Guid courseId, SetCoursePrerequisitesRequest request, CancellationToken cancellationToken = default);

    Task AddAsync(Guid courseId, AddCoursePrerequisiteRequest request, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid courseId, Guid prerequisiteCourseId, CancellationToken cancellationToken = default);
}
