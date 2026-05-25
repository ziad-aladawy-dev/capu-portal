using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// Read + write side for <c>WorkflowDefinition</c>s. Workflows are reusable
/// across services — a single "default-with-payment" workflow can back every
/// service that charges a fee. The service-side <c>StudentServiceService</c>
/// references workflows by id; this contract owns CRUD over them.
/// </summary>
public interface IWorkflowService
{
    Task<WorkflowDefinitionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowDefinitionResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the canonical transition that should fire when a request in
    /// <paramref name="fromStatus"/> is moved to <paramref name="toStatus"/>
    /// under workflow <paramref name="workflowId"/>. Returns null if no
    /// matching transition is configured — the caller throws
    /// <c>ConflictException</c> in that case.
    /// </summary>
    Task<WorkflowTransitionResponse?> ResolveTransitionAsync(
        Guid workflowId,
        ServiceRequestStatus fromStatus,
        ServiceRequestStatus toStatus,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves the configured initial state of a workflow.</summary>
    Task<WorkflowStateResponse?> ResolveInitialStateAsync(Guid workflowId, CancellationToken cancellationToken = default);
}
