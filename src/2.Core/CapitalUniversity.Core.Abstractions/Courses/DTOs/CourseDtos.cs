using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Abstractions.Courses.DTOs;

/// <summary>
/// Read model for <c>Course</c>. Returned by <see cref="ICourseService"/> and
/// safely cacheable per the platform caching strategy
/// (<c>course:object:{id}</c> shared payload).
/// </summary>
public class CourseResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public CourseCategory Category { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCourseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public CourseCategory Category { get; set; } = CourseCategory.Unspecified;
}

public class UpdateCourseRequest
{
    public string? Title { get; set; }
    public int? CreditHours { get; set; }
    public CourseCategory? Category { get; set; }
    public bool? IsActive { get; set; }
}
