using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Domain;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Workflow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default);
    void Update(Workflow workflow);
    Task UpdateWorkflowAsync(Guid workflowId, WorkflowDto updatedWorkflow, CancellationToken cancellationToken = default);
    void Delete(Workflow workflow);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkflowStep?> GetStepByIdAsync(Guid stepId, CancellationToken cancellationToken = default);
    void UpdateStep(WorkflowStep step);
    void DeleteStep(WorkflowStep step);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}