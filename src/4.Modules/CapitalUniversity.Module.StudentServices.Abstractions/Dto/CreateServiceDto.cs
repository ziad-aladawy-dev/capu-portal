using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class CreateServiceDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPaid { get; set; }
    public decimal? Price { get; set; }
    public ServiceScope Scope { get; set; } = new();
    public Guid WorkflowId { get; set; }
    public string FormFieldsJson { get; set; } = "[]";
}