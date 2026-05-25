using CapitalUniversity.Modules.StudentServices.Domain;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public interface IWorkflowRepository
{
    Task<WorkflowDefinition?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default);

    Task<WorkflowDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);
    void Update(WorkflowDefinition workflow);
    void Delete(WorkflowDefinition workflow);
}
