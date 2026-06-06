using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/courses/{externalCourseId}</c>. Field names
/// match the Core <c>Courses.Course</c> shape.
/// </summary>
public sealed class CourseOutboxSeedRequest
{
    public string? Code { get; set; }
    public string? Title { get; set; }
    public int? CreditHours { get; set; }
    public CourseCategory? Category { get; set; }
    public bool? IsActive { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}
