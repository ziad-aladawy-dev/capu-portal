using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;

namespace CapitalUniversity.Core.Abstractions.Courses;

public interface ICourseService
{
    Task<CourseResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CourseResponse>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Paged catalog search with filters + free-text on code/title.</summary>
    Task<PagedResult<CourseResponse>> SearchAsync(CourseSearchQuery query, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>3.9 — bulk delete catalog courses. Per-row commits.</summary>
    Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}
