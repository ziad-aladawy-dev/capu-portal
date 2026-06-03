using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using System.Text.Json;

namespace CapitalUniversity.Module.StudentServices.Application;

public class ServiceManagementService : IServiceManagementService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IStructureNodeRepository _structureNodeRepository;
    private readonly IAcademicYearRepository? _academicYearRepository;

    public ServiceManagementService(
        IServiceRepository serviceRepository,
        IWorkflowRepository workflowRepository,
        IStructureNodeRepository structureNodeRepository,
        IAcademicYearRepository? academicYearRepository = null)
    {
        _serviceRepository = serviceRepository;
        _workflowRepository = workflowRepository;
        _structureNodeRepository = structureNodeRepository;
        _academicYearRepository = academicYearRepository;
    }

    public async Task<ServiceDto> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdWithWorkflowAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");
        return MapToDto(service);
    }

    public async Task<List<ServiceDto>> GetAllActiveServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllActiveAsync(cancellationToken);
        return services.Select(MapToDto).ToList();
    }

    public async Task<List<ServiceDto>> GetAvailableServicesForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await _structureNodeRepository.GetByIdAsync(studentId);
        var studentNodePath = student?.Path;

        Guid? currentYearId = null;
        if (_academicYearRepository != null)
        {
            var currentYear = await _academicYearRepository.GetCurrentAsync();
            currentYearId = currentYear?.Id;
        }

        var services = await _serviceRepository.GetAvailableForStudentAsync(studentId, studentNodePath, currentYearId, cancellationToken);
        return services.Select(MapToDto).ToList();
    }

    public async Task<Guid> CreateServiceAsync(CreateServiceDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.ScopeNodeIds.Any())
        {
            var nodes = await _structureNodeRepository.GetByIdsAsync(dto.ScopeNodeIds);
            if (nodes.Count != dto.ScopeNodeIds.Count)
                throw new NotFoundException("One or more structure nodes not found");
        }

        var workflow = new Workflow
        {
            Name = $"Workflow for {dto.Name}",
            Steps = dto.Workflow.Steps.Select(stepDto => new WorkflowStep
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

        var service = new Service
        {
            Name = dto.Name,
            Type = dto.Type,
            Description = dto.Description,
            IsActive = true,
            IsPaid = dto.IsPaid,
            Price = dto.Price,
            IncludeDescendants = dto.IncludeDescendants,
            AcademicYearId = dto.AcademicYearId,
            WorkflowId = workflow.Id,
            Workflow = workflow
        };

        foreach (var nodeId in dto.ScopeNodeIds)
        {
            service.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = nodeId });
        }

        await _serviceRepository.AddAsync(service, cancellationToken);
        await _serviceRepository.SaveChangesAsync(cancellationToken);
        return service.Id;
    }

    public async Task UpdateServiceAsync(Guid id, UpdateServiceDto dto, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdWithScopeAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");

        if (dto.Name != null) service.Name = dto.Name;
        if (dto.Type.HasValue) service.Type = dto.Type.Value;
        if (dto.Description != null) service.Description = dto.Description;
        if (dto.IsPaid.HasValue) service.IsPaid = dto.IsPaid.Value;
        if (dto.Price.HasValue) service.Price = dto.Price;
        if (dto.IncludeDescendants.HasValue) service.IncludeDescendants = dto.IncludeDescendants.Value;
        if (dto.AcademicYearId.HasValue) service.AcademicYearId = dto.AcademicYearId;
        if (dto.IsActive.HasValue) service.IsActive = dto.IsActive.Value;

        if (dto.ScopeNodeIds != null)
        {
            var existingIds = service.ScopeNodes.Select(sn => sn.StructureNodeId).ToHashSet();
            var newIds = dto.ScopeNodeIds.ToHashSet();

            var toRemove = service.ScopeNodes.Where(sn => !newIds.Contains(sn.StructureNodeId)).ToList();
            foreach (var item in toRemove)
            {
                _serviceRepository.Update(service);
            }

            foreach (var nodeId in newIds.Except(existingIds))
            {
                service.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = nodeId });
            }
        }

        _serviceRepository.Update(service);
        await _serviceRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");

        var inUse = await _serviceRepository.IsServiceInUseAsync(id, cancellationToken);
        if (inUse) throw new ConflictException("Cannot delete service because there are existing requests");

        _serviceRepository.Delete(service);
        await _serviceRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleServiceStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");
        service.IsActive = !service.IsActive;
        _serviceRepository.Update(service);
        await _serviceRepository.SaveChangesAsync(cancellationToken);
    }

    #region Private Mappers

    private ServiceDto MapToDto(Service service)
    {
        return new ServiceDto
        {
            Id = service.Id,
            Name = service.Name,
            Type = service.Type,
            Description = service.Description,
            IsActive = service.IsActive,
            IsPaid = service.IsPaid,
            Price = service.Price,
            ScopeNodeIds = service.ScopeNodes.Select(sn => sn.StructureNodeId).ToList(),
            IncludeDescendants = service.IncludeDescendants,
            AcademicYearId = service.AcademicYearId,
            Workflow = service.Workflow != null ? new WorkflowDto
            {
                Id = service.Workflow.Id,
                Name = service.Workflow.Name,
                Steps = service.Workflow.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto
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
            } : null
        };
    }

    #endregion
}