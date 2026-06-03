using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class WorkflowStepFieldDto
{
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public StepFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public List<string>? Options { get; set; }
}