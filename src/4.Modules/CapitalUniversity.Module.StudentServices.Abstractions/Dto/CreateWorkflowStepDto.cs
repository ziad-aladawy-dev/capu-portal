using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class CreateWorkflowStepDto
{
    public int Order { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public StepInputType InputType { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationRules { get; set; }
    public List<CreateStepActionDto> AvailableActions { get; set; } = new();
}