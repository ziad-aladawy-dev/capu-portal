using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Domain;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.StudentServices.Application;

/// <summary>
/// Owns CRUD over the workflow catalog plus the two resolution paths the
/// request service needs (initial state + transition lookup). Workflows are
/// declarative — no logic is hardcoded inside this class beyond resolving
/// rows.
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkflowRepository _workflows;
    private readonly IValidator<CreateWorkflowDefinitionRequest> _createValidator;

    public WorkflowService(
        IUnitOfWork unitOfWork,
        IWorkflowRepository workflows,
        IValidator<CreateWorkflowDefinitionRequest> createValidator)
    {
        _unitOfWork = unitOfWork;
        _workflows = workflows;
        _createValidator = createValidator;
    }

    public async Task<WorkflowDefinitionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetByIdAsync(id, includeChildren: true, cancellationToken);
        return workflow is null ? null : MapToResponse(workflow);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _workflows.GetAllAsync(cancellationToken)).Select(MapToResponse).ToList();

    public async Task<Guid> CreateAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        if (await _workflows.ExistsByCodeAsync(request.Code, excludeId: null, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.StudentServices.WorkflowCodeInUse);
        }

        var workflow = new WorkflowDefinition
        {
            Code = request.Code,
            Name = LocalizedJson.Normalize(request.Name),
            Description = LocalizedJson.Normalize(request.Description),
        };
        foreach (var s in request.States)
        {
            workflow.States.Add(new WorkflowState
            {
                Status = s.Status,
                DisplayOrder = s.DisplayOrder,
                IsInitial = s.IsInitial,
                IsTerminal = s.IsTerminal,
                IsWaitingPayment = s.IsWaitingPayment,
            });
        }
        foreach (var t in request.Transitions)
        {
            workflow.Transitions.Add(new WorkflowTransition
            {
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                TransitionType = t.TransitionType,
                RequiredAction = t.RequiredAction ?? string.Empty,
            });
        }
        await _workflows.AddAsync(workflow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return workflow.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetByIdAsync(id, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.WorkflowNotFound);
        _workflows.Delete(workflow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowTransitionResponse?> ResolveTransitionAsync(
        Guid workflowId,
        ServiceRequestStatus fromStatus,
        ServiceRequestStatus toStatus,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetByIdAsync(workflowId, includeChildren: true, cancellationToken);
        if (workflow is null) return null;
        var transition = workflow.Transitions
            .FirstOrDefault(t => t.FromStatus == fromStatus && t.ToStatus == toStatus);
        return transition is null ? null : MapTransition(transition);
    }

    public async Task<WorkflowStateResponse?> ResolveInitialStateAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflows.GetByIdAsync(workflowId, includeChildren: true, cancellationToken);
        var initial = workflow?.States.FirstOrDefault(s => s.IsInitial);
        return initial is null ? null : MapState(initial);
    }

    private static WorkflowDefinitionResponse MapToResponse(WorkflowDefinition w) => new()
    {
        Id = w.Id,
        Code = w.Code,
        Name = w.Name,
        Description = w.Description,
        States = w.States.OrderBy(s => s.DisplayOrder).Select(MapState).ToList(),
        Transitions = w.Transitions.Select(MapTransition).ToList(),
    };

    private static WorkflowStateResponse MapState(WorkflowState s) => new()
    {
        Id = s.Id,
        Status = s.Status,
        DisplayOrder = s.DisplayOrder,
        IsInitial = s.IsInitial,
        IsTerminal = s.IsTerminal,
        IsWaitingPayment = s.IsWaitingPayment,
    };

    private static WorkflowTransitionResponse MapTransition(WorkflowTransition t) => new()
    {
        Id = t.Id,
        FromStatus = t.FromStatus,
        ToStatus = t.ToStatus,
        TransitionType = t.TransitionType,
        RequiredAction = t.RequiredAction,
    };

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
