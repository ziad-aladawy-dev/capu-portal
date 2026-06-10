using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly StudentServicesDbContext _context;

    public WorkflowRepository(StudentServicesDbContext context)
    {
        _context = context;
    }

    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.Fields)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<List<Workflow>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.Fields)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default)
        => await _context.Workflows.AddAsync(workflow, cancellationToken);

    public void Update(Workflow workflow) => _context.Workflows.Update(workflow);

    public async Task UpdateWorkflowAsync(Guid workflowId, WorkflowDto updatedWorkflow, CancellationToken cancellationToken = default)
    {
        var workflow = await _context.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.Fields)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow == null) throw new NotFoundException("Workflow not found");

        if (!string.IsNullOrEmpty(updatedWorkflow.Name))
            workflow.Name = updatedWorkflow.Name;

        var existingSteps = workflow.Steps.ToList();
        var incomingSteps = updatedWorkflow.Steps.OrderBy(s => s.Order).ToList();

        var stepsToRemove = existingSteps.Where(e => !incomingSteps.Any(i => i.Order == e.Order)).ToList();
        foreach (var step in stepsToRemove)
        {
            _context.WorkflowSteps.Remove(step);
        }

        foreach (var stepDto in incomingSteps)
        {
            var existingStep = existingSteps.FirstOrDefault(e => e.Order == stepDto.Order);

            if (existingStep != null)
            {
                existingStep.Title = stepDto.Title;
                existingStep.Description = stepDto.Description;
                existingStep.StepType = stepDto.StepType;
                existingStep.IsRequired = stepDto.IsRequired;

                var existingFields = existingStep.Fields.ToList();
                var incomingFields = stepDto.Fields.OrderBy(f => f.Order).ToList();

                var fieldsToRemove = existingFields.Where(ef => !incomingFields.Any(inf => inf.Order == ef.Order)).ToList();
                foreach (var field in fieldsToRemove)
                {
                    _context.WorkflowStepFields.Remove(field);
                }

                foreach (var fieldDto in incomingFields)
                {
                    var existingField = existingFields.FirstOrDefault(ef => ef.Order == fieldDto.Order);
                    if (existingField != null)
                    {
                        existingField.Label = fieldDto.Label;
                        existingField.FieldType = fieldDto.FieldType;
                        existingField.IsRequired = fieldDto.IsRequired;
                        existingField.OptionsJson = fieldDto.Options != null && fieldDto.Options.Any()
                            ? JsonSerializer.Serialize(fieldDto.Options)
                            : null;
                    }
                    else
                    {
                        existingStep.Fields.Add(new WorkflowStepField
                        {
                            Order = fieldDto.Order,
                            Label = fieldDto.Label,
                            FieldType = fieldDto.FieldType,
                            IsRequired = fieldDto.IsRequired,
                            OptionsJson = fieldDto.Options != null && fieldDto.Options.Any()
                                ? JsonSerializer.Serialize(fieldDto.Options)
                                : null
                        });
                    }
                }
            }
            else
            {
                var newStep = new WorkflowStep
                {
                    Order = stepDto.Order,
                    Title = stepDto.Title,
                    Description = stepDto.Description,
                    StepType = stepDto.StepType,
                    IsRequired = stepDto.IsRequired,
                    WorkflowId = workflow.Id
                };
                foreach (var fieldDto in stepDto.Fields.OrderBy(f => f.Order))
                {
                    newStep.Fields.Add(new WorkflowStepField
                    {
                        Order = fieldDto.Order,
                        Label = fieldDto.Label,
                        FieldType = fieldDto.FieldType,
                        IsRequired = fieldDto.IsRequired,
                        OptionsJson = fieldDto.Options != null && fieldDto.Options.Any()
                            ? JsonSerializer.Serialize(fieldDto.Options)
                            : null
                    });
                }
                _context.WorkflowSteps.Add(newStep);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public void Delete(Workflow workflow) => _context.Workflows.Remove(workflow);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows.AnyAsync(w => w.Id == id, cancellationToken);

    public async Task<WorkflowStep?> GetStepByIdAsync(Guid stepId, CancellationToken cancellationToken = default)
        => await _context.WorkflowSteps
            .Include(s => s.Fields)
            .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken);

    public void UpdateStep(WorkflowStep step) => _context.WorkflowSteps.Update(step);

    public void DeleteStep(WorkflowStep step) => _context.WorkflowSteps.Remove(step);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}