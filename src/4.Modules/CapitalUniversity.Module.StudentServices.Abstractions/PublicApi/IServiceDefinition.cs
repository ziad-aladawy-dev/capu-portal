namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public interface IServiceDefinition
{
    Guid Id { get; }
    string Name { get; }
    string? Description { get; }
    bool IsActive { get; }
    bool IsPaid { get; }
    decimal? Price { get; }
    ServiceType Type { get; }
    IReadOnlyList<Guid> ScopeNodeIds { get; }
    bool IncludeDescendants { get; }
    Guid? AcademicYearId { get; }
    IWorkflowDefinition Workflow { get; }
}