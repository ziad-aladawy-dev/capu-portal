using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class ServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ServiceType Type { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaid { get; set; }
    public decimal? Price { get; set; }
    public List<Guid> ScopeNodeIds { get; set; } = new();
    public List<ScopeNodeDetailsDto> ScopeNodesDetails { get; set; } = new();
    public bool IncludeDescendants { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? SemesterId { get; set; }
    public int? LevelOrder { get; set; }
    public WorkflowDto? Workflow { get; set; }
}