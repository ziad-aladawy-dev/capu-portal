using CapitalUniversity.Module.StudentServices.Abstractions.Dto;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IWorkflowManagementService
{
    Task<WorkflowDto> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<WorkflowDto>> GetAllWorkflowsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateWorkflowAsync(CreateWorkflowDto dto, CancellationToken cancellationToken = default);
    Task UpdateWorkflowAsync(Guid id, UpdateWorkflowDto dto, CancellationToken cancellationToken = default);
    Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> AddStepAsync(Guid workflowId, CreateWorkflowStepDto dto, CancellationToken cancellationToken = default);
    Task UpdateStepAsync(Guid stepId, UpdateWorkflowStepDto dto, CancellationToken cancellationToken = default);
    Task DeleteStepAsync(Guid stepId, CancellationToken cancellationToken = default);
}