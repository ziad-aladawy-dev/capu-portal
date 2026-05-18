using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// Join-row binding a <see cref="Course"/> into an <see cref="AcademicPlan"/>
/// at a specific level + semester slot, with a mandatory/elective flag. Holds
/// the curriculum-composition data only — never prerequisite chains, blocking
/// rules, or enrollment state (those belong to a future Registration module).
/// </summary>
public class AcademicPlanCourse : BaseEntity
{
    public Guid AcademicPlanId { get; set; }
    public AcademicPlan? AcademicPlan { get; set; }

    /// <summary>Catalog course referenced by ID — no navigation, the catalog is read-through service-side.</summary>
    public Guid CourseId { get; set; }

    /// <summary>Academic level (e.g. 1..4 for a 4-year program). Plan-relative.</summary>
    public int Level { get; set; }

    /// <summary>Ordinal semester within the level (1..N). Plan-relative.</summary>
    public int Semester { get; set; }

    /// <summary>True if the course is required to complete the plan; false for electives.</summary>
    public bool IsMandatory { get; set; }
}
