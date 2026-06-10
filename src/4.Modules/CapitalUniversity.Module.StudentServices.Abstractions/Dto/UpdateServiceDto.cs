using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class UpdateServiceDto
{
    public string? Name { get; set; }
    public ServiceType? Type { get; set; }
    public string? Description { get; set; }
    public bool? IsPaid { get; set; }
    public decimal? Price { get; set; }
    public List<Guid>? ScopeNodeIds { get; set; }
    public bool? IncludeDescendants { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
    public int? LevelOrder { get; set; }
    public bool? IsActive { get; set; }
    public WorkflowDto? Workflow { get; set; }
}