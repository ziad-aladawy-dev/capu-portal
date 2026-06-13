namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public class WorkflowStepDefinition
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; }
    public bool IsRequired { get; set; }
    public decimal? Price { get; set; }
    public List<WorkflowStepFieldDefinition> Fields { get; set; } = new();
}