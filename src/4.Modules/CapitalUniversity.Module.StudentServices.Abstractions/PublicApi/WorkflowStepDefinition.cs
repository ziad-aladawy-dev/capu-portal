namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public class WorkflowStepDefinition
{
    public int Order { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public StepInputType InputType { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationRules { get; set; }
    public List<StepAction> AvailableActions { get; set; } = new();
}