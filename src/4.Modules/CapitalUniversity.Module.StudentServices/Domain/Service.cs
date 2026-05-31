using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class Service : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPaid { get; set; }
    public decimal? Price { get; set; }
    public ServiceScope Scope { get; set; } = new();
    public string FormFieldsJson { get; set; } = "[]";

    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
}