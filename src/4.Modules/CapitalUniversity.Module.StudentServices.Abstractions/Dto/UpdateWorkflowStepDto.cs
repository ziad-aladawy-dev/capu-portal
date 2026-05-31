using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class UpdateWorkflowStepDto
{
    public int? Order { get; set; }
    public string? StepKey { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public StepInputType? InputType { get; set; }
    public bool? IsRequired { get; set; }
    public string? ValidationRules { get; set; }
}