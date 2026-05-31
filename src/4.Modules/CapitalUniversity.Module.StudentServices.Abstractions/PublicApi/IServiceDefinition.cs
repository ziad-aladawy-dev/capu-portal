namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public interface IServiceDefinition
{
    Guid Id { get; }
    string Name { get; }
    string? Description { get; }
    bool IsActive { get; }
    bool IsPaid { get; }
    decimal? Price { get; }
    ServiceScope Scope { get; }
    IWorkflowDefinition Workflow { get; }
    string FormFieldsJson { get; }
}