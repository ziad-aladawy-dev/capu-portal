namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class WorkflowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<WorkflowStepDto> Steps { get; set; } = new();
}