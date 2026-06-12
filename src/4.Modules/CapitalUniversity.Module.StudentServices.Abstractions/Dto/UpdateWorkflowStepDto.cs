using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class UpdateWorkflowStepDto
{
    public int? Order { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public WorkflowStepType? StepType { get; set; }
    public WorkflowTransitionType? TransitionType { get; set; }
    public bool? IsRequired { get; set; }
}