namespace CapitalUniversity.Core.Abstractions.Courses.DTOs;

public class AcademicPlanResponse
{
    public Guid Id { get; set; }
    public Guid StructureNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public List<AcademicPlanCourseResponse> PlanCourses { get; set; } = new();
}

public class AcademicPlanCourseResponse
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public int Level { get; set; }
    public int Semester { get; set; }
    public bool IsMandatory { get; set; }
}

public class CreateAcademicPlanRequest
{
    public Guid StructureNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class UpdateAcademicPlanRequest
{
    public string? Name { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool? IsActive { get; set; }
}

public class AddPlanCourseRequest
{
    public Guid CourseId { get; set; }
    public int Level { get; set; }
    public int Semester { get; set; }
    public bool IsMandatory { get; set; }
}

/// <summary>
/// Atomic add/remove diff applied to a plan's composition. The whole batch
/// commits in a single transaction or none of it does — partial application
/// would leave the plan in an inconsistent state (e.g. a curriculum revision
/// landing only the additions but not the removals).
/// </summary>
public class BatchPlanCoursesRequest
{
    public IReadOnlyList<AddPlanCourseRequest> Add { get; set; } = Array.Empty<AddPlanCourseRequest>();

    /// <summary>Ids of <c>AcademicPlanCourse</c> entries to remove from the plan.</summary>
    public IReadOnlyList<Guid> Remove { get; set; } = Array.Empty<Guid>();
}

/// <summary>
/// Academic-plan search. Sort whitelist: <c>name</c>, <c>effectiveFrom</c>, <c>createdAt</c>.
/// Default order: <c>effectiveFrom:desc</c>.
/// </summary>
public class AcademicPlanSearchQuery : Shared.Paging.PagedQueryRequest
{
    public Guid? StructureNodeId { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? EffectiveFromInclusive { get; set; }
    public DateTime? EffectiveToExclusive { get; set; }
}
