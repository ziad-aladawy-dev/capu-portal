namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class CreateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public List<CreateWorkflowStepDto> Steps { get; set; } = new();
}