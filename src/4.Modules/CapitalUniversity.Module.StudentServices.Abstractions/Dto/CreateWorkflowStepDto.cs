using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class CreateWorkflowStepDto
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.Form;
    public bool IsRequired { get; set; } = true;
    public List<CreateStepFieldDto> Fields { get; set; } = new();
}