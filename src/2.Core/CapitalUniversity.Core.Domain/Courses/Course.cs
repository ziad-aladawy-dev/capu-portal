using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// Catalog course entity — the stable, school-wide definition of a deliverable
/// unit of study. Per the platform plan: Courses module owns the catalog only;
/// prerequisites, enrollment, transcript, and GPA logic belong to a future
/// Registration module and MUST NOT live on this entity.
/// </summary>
public class Course : BaseEntity
{
    private string _code = string.Empty;

    /// <summary>Unique short code (e.g. <c>"CS101"</c>). Trimmed and upper-cased on set so the catalog cannot drift on whitespace or casing.</summary>
    public string Code
    {
        get => _code;
        set => _code = (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>Human-readable title (e.g. <c>"Introduction to Algorithms"</c>).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Credit hours awarded on successful completion. Non-negative.</summary>
    public int CreditHours { get; set; }

    /// <summary>Category — see <see cref="CourseCategory"/>.</summary>
    public CourseCategory Category { get; set; } = CourseCategory.Unspecified;

    /// <summary>Soft-disable flag. Inactive courses stay in the catalog for history but no longer surface in active plans.</summary>
    public bool IsActive { get; set; } = true;
}
