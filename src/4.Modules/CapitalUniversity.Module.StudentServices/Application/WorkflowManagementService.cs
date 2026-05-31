using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Application;

public class WorkflowManagementService : IWorkflowManagementService
{
    private readonly StudentServicesDbContext _context;

    public WorkflowManagementService(StudentServicesDbContext context)
    {
        _context = context;
    }

    public async Task<IWorkflowDefinition> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _context.Workflows
            .Include(x => x.Steps)
                .ThenInclude(s => s.AvailableActions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");
        return new WorkflowDefinitionAdapter(workflow);
    }

    public async Task<IEnumerable<IWorkflowDefinition>> GetAllWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var workflows = await _context.Workflows
            .Include(x => x.Steps)
                .ThenInclude(s => s.AvailableActions)
            .ToListAsync(cancellationToken);
        return workflows.Select(w => new WorkflowDefinitionAdapter(w)).ToList();
    }

    public async Task<Guid> CreateWorkflowAsync(CreateWorkflowDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = new Workflow { Name = dto.Name };
        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync(cancellationToken);
        return workflow.Id;
    }

    public async Task UpdateWorkflowAsync(Guid id, UpdateWorkflowDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = await _context.Workflows.FindAsync(new object[] { id }, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");
        if (dto.Name != null) workflow.Name = dto.Name;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _context.Workflows.FindAsync(new object[] { id }, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");

        var isUsed = await _context.Services.AnyAsync(s => s.WorkflowId == id, cancellationToken);
        if (isUsed) throw new ConflictException("Cannot delete workflow because it is used by one or more services");

        _context.Workflows.Remove(workflow);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddStepAsync(Guid workflowId, CreateWorkflowStepDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = await _context.Workflows.FindAsync(new object[] { workflowId }, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");

        var step = new WorkflowStep
        {
            WorkflowId = workflowId,
            Order = dto.Order,
            StepKey = dto.StepKey,
            Title = dto.Title,
            Description = dto.Description,
            InputType = dto.InputType,
            IsRequired = dto.IsRequired,
            ValidationRules = dto.ValidationRules,
            AvailableActions = dto.AvailableActions.Select(a => new WorkflowStepAction
            {
                ActionKey = a.ActionKey,
                Label = a.Label,
                TriggersSubmission = a.TriggersSubmission
            }).ToList()
        };

        _context.WorkflowSteps.Add(step);
        await _context.SaveChangesAsync(cancellationToken);
        return step.Id;
    }

    public async Task UpdateStepAsync(Guid stepId, UpdateWorkflowStepDto dto, CancellationToken cancellationToken = default)
    {
        var step = await _context.WorkflowSteps.FindAsync(new object[] { stepId }, cancellationToken);
        if (step == null) throw new NotFoundException("Workflow step not found");

        if (dto.Order.HasValue) step.Order = dto.Order.Value;
        if (dto.StepKey != null) step.StepKey = dto.StepKey;
        if (dto.Title != null) step.Title = dto.Title;
        if (dto.Description != null) step.Description = dto.Description;
        if (dto.InputType.HasValue) step.InputType = dto.InputType.Value;
        if (dto.IsRequired.HasValue) step.IsRequired = dto.IsRequired.Value;
        if (dto.ValidationRules != null) step.ValidationRules = dto.ValidationRules;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteStepAsync(Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await _context.WorkflowSteps.FindAsync(new object[] { stepId }, cancellationToken);
        if (step == null) throw new NotFoundException("Workflow step not found");

        _context.WorkflowSteps.Remove(step);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private class WorkflowDefinitionAdapter : IWorkflowDefinition
    {
        private readonly Workflow _workflow;
        public WorkflowDefinitionAdapter(Workflow workflow) => _workflow = workflow;
        public Guid Id => _workflow.Id;
        public string Name => _workflow.Name;
        public List<WorkflowStepDefinition> Steps => _workflow.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDefinition
        {
            Order = s.Order,
            StepKey = s.StepKey,
            Title = s.Title,
            Description = s.Description,
            InputType = s.InputType,
            IsRequired = s.IsRequired,
            ValidationRules = s.ValidationRules,
            AvailableActions = s.AvailableActions.Select(a => new StepAction
            {
                ActionKey = a.ActionKey,
                Label = a.Label,
                TriggersSubmission = a.TriggersSubmission
            }).ToList()
        }).ToList();
    }
}