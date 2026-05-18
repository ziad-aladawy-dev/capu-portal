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
