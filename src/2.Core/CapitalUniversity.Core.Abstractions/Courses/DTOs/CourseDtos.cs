using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Abstractions.Courses.DTOs;

/// <summary>
/// Read model for <c>Course</c>. Returned by <see cref="ICourseService"/> and
/// safely cacheable per the platform caching strategy
/// (<c>course:object:{id}</c> shared payload). <c>Title</c> is decoded
/// against the caller's culture by the service before returning.
/// </summary>
public class CourseResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public CourseCategory Category { get; set; }
    public bool IsActive { get; set; }
    public bool IsClosed { get; set; }
}

/// <summary>
/// Write model. <c>Title</c> is a free-text string — operators send either
/// the canonical <c>{"ar":"…","en":"…"}</c> JSON for a bilingual catalog
/// entry or a plain literal for single-language data. The resolver round-trips
/// both shapes safely on read. <c>Code</c> is language-neutral plain text
/// (e.g. <c>"CS101"</c>, max 32 chars) and is stored verbatim — never
/// localized JSON.
/// </summary>
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
