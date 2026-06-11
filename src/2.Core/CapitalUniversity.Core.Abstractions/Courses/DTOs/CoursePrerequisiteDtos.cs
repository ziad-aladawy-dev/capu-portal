namespace CapitalUniversity.Core.Abstractions.Courses.DTOs;

/// <summary>
/// Enriched read model for one prerequisite of a specific course. The service
/// joins the catalog so callers get display fields without N+1 course lookups.
/// <c>Title</c> is decoded against the caller's culture before returning.
/// </summary>
public class CoursePrerequisiteResponse
{
    public Guid PrerequisiteCourseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Raw directed edge (<c>CourseId</c> requires <c>PrerequisiteCourseId</c>).
/// Returned by the whole-graph endpoint so clients can run cycle checks and
/// catalog-health cross-references locally.
/// </summary>
public class CoursePrerequisiteLinkResponse
{
    public Guid CourseId { get; set; }
    public Guid PrerequisiteCourseId { get; set; }
}

/// <summary>Batch-replace write model: the full new prerequisite set for a course.</summary>
public class SetCoursePrerequisitesRequest
{
    public List<Guid> PrerequisiteCourseIds { get; set; } = new();
}

/// <summary>Single-edge write model.</summary>
public class AddCoursePrerequisiteRequest
{
    public Guid PrerequisiteCourseId { get; set; }
}
