namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public interface IWorkflowDefinition
{
    Guid Id { get; }
    string Name { get; }
    List<WorkflowStepDefinition> Steps { get; }
}