using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class Service : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ServiceType Type { get; set; } = ServiceType.General;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPaid { get; set; }
    public decimal? Price { get; set; }
    public Guid? AcademicYearId { get; set; }

    public ICollection<ServiceStructureNode> ScopeNodes { get; set; } = new List<ServiceStructureNode>();
    public bool IncludeDescendants { get; set; } = true;

    public Guid? WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }
}