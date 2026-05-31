using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class UpdateServiceDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsPaid { get; set; }
    public decimal? Price { get; set; }
    public ServiceScope? Scope { get; set; }
    public Guid? WorkflowId { get; set; }
    public bool? IsActive { get; set; }
    public string? FormFieldsJson { get; set; }
}