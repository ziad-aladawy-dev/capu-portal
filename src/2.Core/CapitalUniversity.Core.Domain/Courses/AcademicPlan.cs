using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// An academic plan owned by a single <c>StructureNode</c> (typically a Program
/// or Department). Plans declare which catalog courses are part of the
/// curriculum and at what level/semester via <see cref="AcademicPlanCourse"/>;
/// they do NOT model registration, prerequisites, or transcripts (out of scope
/// per <c>docs/Plan.md</c>).
///
/// <para>
/// <see cref="EffectiveFrom"/> and <see cref="EffectiveTo"/> let a school
/// version its plans (e.g. catalog year). At most one plan is typically
/// <see cref="IsActive"/>=true per structure node — enforced at the application
/// layer, not at the schema level.
/// </para>
/// </summary>
public class AcademicPlan : BaseEntity
{
    /// <summary>Owning structure node (Program or Department). FK without navigation — cross-module reference by ID per the modularity rule.</summary>
    public Guid StructureNodeId { get; set; }

    /// <summary>SQL Server <c>rowversion</c>. EF maps this to optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>Human-readable name (e.g. <c>"BSc Computer Science 2025"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Earliest catalog year this plan applies to. Inclusive.</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Latest catalog year this plan applies to. Inclusive. Null = open-ended.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Active flag — surfaces in plan pickers when true.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The plan's course composition.</summary>
    public ICollection<AcademicPlanCourse> PlanCourses { get; set; } = new List<AcademicPlanCourse>();
}
