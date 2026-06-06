using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Sync.Courses.Domain;

/// <summary>
/// External shape from the upstream catalog system. Maps to Core's
/// <c>Courses.Course</c> field set: bilingual Code + Title, integer
/// <c>CreditHours</c>, <c>Category</c> (Core's enum, used directly — no local
/// mirror needed since Sync.Courses now references Core.Domain).
/// </summary>
public sealed class ExternalCourse
{
    public required string ExternalCourseId { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required int CreditHours { get; init; }
    public required CourseCategory Category { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}
