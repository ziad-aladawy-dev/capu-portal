using CapitalUniversity.Core.Abstractions.Courses.DTOs;

namespace CapitalUniversity.Core.Abstractions.Courses;

public interface ICourseService
{
    Task<CourseResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CourseResponse>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);
}
