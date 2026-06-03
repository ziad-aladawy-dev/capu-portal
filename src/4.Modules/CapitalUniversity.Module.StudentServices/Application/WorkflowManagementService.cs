using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using System.Text.Json;

namespace CapitalUniversity.Module.StudentServices.Application;

public class WorkflowManagementService : IWorkflowManagementService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IServiceRepository _serviceRepository;

    public WorkflowManagementService(
        IWorkflowRepository workflowRepository,
        IServiceRepository serviceRepository)
    {
        _workflowRepository = workflowRepository;
        _serviceRepository = serviceRepository;
    }

    public async Task<WorkflowDto> GetWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdWithStepsAsync(id, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");
        return MapToDto(workflow);
    }

    public async Task<List<WorkflowDto>> GetAllWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var workflows = await _workflowRepository.GetAllAsync(cancellationToken);
        return workflows.Select(MapToDto).ToList();
    }

    public async Task<Guid> CreateWorkflowAsync(CreateWorkflowDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = new Workflow
        {
            Name = dto.Name,
            Steps = dto.Steps.Select(stepDto => new WorkflowStep
            {
                Order = stepDto.Order,
                Title = stepDto.Title,
                Description = stepDto.Description,
                StepType = stepDto.StepType,
                IsRequired = stepDto.IsRequired,
                Fields = stepDto.Fields.Select(fieldDto => new WorkflowStepField
                {
                    Order = fieldDto.Order,
                    Label = fieldDto.Label,
                    FieldType = fieldDto.FieldType,
                    IsRequired = fieldDto.IsRequired,
                    OptionsJson = fieldDto.Options != null ? JsonSerializer.Serialize(fieldDto.Options) : null
                }).ToList()
            }).ToList()
        };

        await _workflowRepository.AddAsync(workflow, cancellationToken);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
        return workflow.Id;
    }

    public async Task UpdateWorkflowAsync(Guid id, UpdateWorkflowDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");
        if (dto.Name != null) workflow.Name = dto.Name;
        _workflowRepository.Update(workflow);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");

        var isUsed = await _serviceRepository.IsServiceInUseByWorkflowAsync(id, cancellationToken);
        if (isUsed) throw new ConflictException("Cannot delete workflow because it is used by one or more services");

        _workflowRepository.Delete(workflow);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AddStepAsync(Guid workflowId, CreateWorkflowStepDto dto, CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
        if (workflow == null) throw new NotFoundException("Workflow not found");

        var step = new WorkflowStep
        {
            WorkflowId = workflowId,
            Order = dto.Order,
            Title = dto.Title,
            Description = dto.Description,
            StepType = dto.StepType,
            IsRequired = dto.IsRequired,
            Fields = dto.Fields.Select(fieldDto => new WorkflowStepField
            {
                Order = fieldDto.Order,
                Label = fieldDto.Label,
                FieldType = fieldDto.FieldType,
                IsRequired = fieldDto.IsRequired,
                OptionsJson = fieldDto.Options != null ? JsonSerializer.Serialize(fieldDto.Options) : null
            }).ToList()
        };

        workflow.Steps.Add(step);
        _workflowRepository.Update(workflow);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
        return step.Id;
    }

    public async Task UpdateStepAsync(Guid stepId, UpdateWorkflowStepDto dto, CancellationToken cancellationToken = default)
    {
        var step = await _workflowRepository.GetStepByIdAsync(stepId, cancellationToken);
        if (step == null) throw new NotFoundException("Workflow step not found");

        if (dto.Order.HasValue) step.Order = dto.Order.Value;
        if (dto.Title != null) step.Title = dto.Title;
        if (dto.Description != null) step.Description = dto.Description;
        if (dto.StepType.HasValue) step.StepType = dto.StepType.Value;
        if (dto.IsRequired.HasValue) step.IsRequired = dto.IsRequired.Value;

        _workflowRepository.UpdateStep(step);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteStepAsync(Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await _workflowRepository.GetStepByIdAsync(stepId, cancellationToken);
        if (step == null) throw new NotFoundException("Workflow step not found");

        _workflowRepository.DeleteStep(step);
        await _workflowRepository.SaveChangesAsync(cancellationToken);
    }

    #region Private Mappers

    private WorkflowDto MapToDto(Workflow workflow)
    {
        return new WorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto
            {
                Order = s.Order,
                Title = s.Title,
                Description = s.Description,
                StepType = s.StepType,
                IsRequired = s.IsRequired,
                Fields = s.Fields.OrderBy(f => f.Order).Select(f => new WorkflowStepFieldDto
                {
                    Order = f.Order,
                    Label = f.Label,
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired,
                    Options = f.OptionsJson != null ? JsonSerializer.Deserialize<List<string>>(f.OptionsJson) : null
                }).ToList()
            }).ToList()
        };
    }

    #endregion
}