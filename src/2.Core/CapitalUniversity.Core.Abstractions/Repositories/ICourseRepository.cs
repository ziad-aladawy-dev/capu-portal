using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Course?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Course course, CancellationToken cancellationToken = default);
    void Update(Course course);
    void Delete(Course course);

    /// <summary>
    /// True if any <see cref="AcademicPlanCourse"/> composition row references
    /// this course. Used as a delete usage-guard so a referenced catalog course
    /// cannot be removed out from under a plan (the DB FK is the schema-level
    /// backstop; this gives a clean Conflict instead of a raw FK violation).
    /// </summary>
    Task<bool> IsReferencedByPlanAsync(Guid courseId, CancellationToken cancellationToken = default);

    /// <summary>Paged catalog search with filters + free-text on code/title.</summary>
    Task<PagedResult<Course>> SearchAsync(CourseSearchQuery query, CancellationToken cancellationToken = default);
}
