using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
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
    private readonly ILocalizationService _localizationService;
    private readonly IAcademicYearRepository? _academicYearRepository;
    private readonly ISemesterRepository? _semesterRepository;
    private readonly IStudentRepository _studentRepository;

    public ServiceManagementService(
        IServiceRepository serviceRepository,
        IWorkflowRepository workflowRepository,
        IStructureNodeRepository structureNodeRepository,
        ILocalizationService localizationService,
        IStudentRepository studentRepository,
        IAcademicYearRepository? academicYearRepository = null,
        ISemesterRepository? semesterRepository = null)
    {
        _serviceRepository = serviceRepository;
        _workflowRepository = workflowRepository;
        _structureNodeRepository = structureNodeRepository;
        _localizationService = localizationService;
        _studentRepository = studentRepository;
        _academicYearRepository = academicYearRepository;
        _semesterRepository = semesterRepository;
    }

    public async Task<ServiceDto> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdWithWorkflowAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");
        var nodeIds = service.ScopeNodes.Select(sn => sn.StructureNodeId).ToList();
        var nodes = nodeIds.Any() ? await _structureNodeRepository.GetByIdsAsync(nodeIds) : new List<StructureNode>();
        return await MapToDtoAsync(service, nodes);
    }

    public async Task<List<ServiceDto>> GetAllActiveServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllActiveAsync(cancellationToken);
        return await MapServicesToDtosAsync(services, cancellationToken);
    }

    public async Task<List<ServiceDto>> GetAllServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        return await MapServicesToDtosAsync(services, cancellationToken);
    }

    public async Task<List<ServiceDto>> GetAvailableServicesForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student == null) throw new NotFoundException("Student not found");

        string? studentNodePath = null;
        int? studentLevelNumber = null;
        StructureNode? structureNode = null;

        if (student.StructureNodeId != Guid.Empty)
        {
            structureNode = await _structureNodeRepository.GetByIdAsync(student.StructureNodeId);
            if (structureNode != null)
            {
                studentNodePath = structureNode.Path;

                if (structureNode.Type == StructureNodeType.Level)
                {
                    studentLevelNumber = structureNode.Order;
                }
                else
                {
                    var parent = structureNode.Parent;
                    while (parent != null)
                    {
                        if (parent.Type == StructureNodeType.Level)
                        {
                            studentLevelNumber = parent.Order;
                            break;
                        }
                        parent = parent.Parent;
                    }
                }
            }
        }

        Guid? currentYearId = null;
        if (_academicYearRepository != null)
        {
            var currentYear = await _academicYearRepository.GetCurrentAsync();
            currentYearId = currentYear?.Id;
        }

        Guid? currentSemesterId = null;
        if (_semesterRepository != null)
        {
            var currentSemester = await _semesterRepository.GetCurrentAsync();
            currentSemesterId = currentSemester?.Id;
        }

        var services = await _serviceRepository.GetAvailableForStudentAsync(
            studentId,
            studentNodePath,
            studentLevelNumber,
            currentYearId,
            currentSemesterId,
            cancellationToken);

        return await MapServicesToDtosAsync(services, cancellationToken);
    }

    public async Task<Guid> CreateServiceAsync(CreateServiceDto dto, CancellationToken cancellationToken = default)
    {
        // Validate workflow step types
        foreach (var step in dto.Workflow.Steps)
        {
            if (step.StepType != WorkflowStepType.Form &&
                step.StepType != WorkflowStepType.Review &&
                step.StepType != WorkflowStepType.Payment)
                throw new ValidationException($"Invalid step type: {step.StepType}");
        }

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
                TransitionType = stepDto.TransitionType,
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
            SemesterId = dto.SemesterId,
            LevelOrder = dto.LevelOrder,
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
        if (dto.SemesterId.HasValue) service.SemesterId = dto.SemesterId;
        if (dto.LevelOrder.HasValue) service.LevelOrder = dto.LevelOrder.Value;
        if (dto.IsActive.HasValue) service.IsActive = dto.IsActive.Value;

        if (dto.Workflow != null)
        {
            // Validate new workflow step types
            foreach (var step in dto.Workflow.Steps)
            {
                if (step.StepType != WorkflowStepType.Form &&
                    step.StepType != WorkflowStepType.Review &&
                    step.StepType != WorkflowStepType.Payment)
                    throw new ValidationException($"Invalid step type: {step.StepType}");
            }

            if (service.WorkflowId.HasValue)
            {
                var oldWorkflow = await _workflowRepository.GetByIdWithStepsAsync(service.WorkflowId.Value, cancellationToken);
                if (oldWorkflow != null)
                {
                    _workflowRepository.Delete(oldWorkflow);
                }
            }

            var newWorkflow = new Workflow
            {
                Name = $"Workflow for {service.Name}",
                Steps = dto.Workflow.Steps.Select(stepDto => new WorkflowStep
                {
                    Order = stepDto.Order,
                    Title = stepDto.Title,
                    Description = stepDto.Description,
                    StepType = stepDto.StepType,
                    TransitionType = stepDto.TransitionType,
                    IsRequired = stepDto.IsRequired,
                    Fields = stepDto.Fields.Select(fieldDto => new WorkflowStepField
                    {
                        Order = fieldDto.Order,
                        Label = fieldDto.Label,
                        FieldType = fieldDto.FieldType,
                        IsRequired = fieldDto.IsRequired,
                        OptionsJson = fieldDto.Options != null && fieldDto.Options.Any()
                            ? JsonSerializer.Serialize(fieldDto.Options)
                            : null
                    }).ToList()
                }).ToList()
            };

            await _workflowRepository.AddAsync(newWorkflow, cancellationToken);
            await _workflowRepository.SaveChangesAsync(cancellationToken);
            service.WorkflowId = newWorkflow.Id;
        }

        if (dto.ScopeNodeIds != null)
        {
            await _serviceRepository.UpdateServiceScopeNodesAsync(id, dto.ScopeNodeIds, cancellationToken);
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

    private async Task<List<ServiceDto>> MapServicesToDtosAsync(List<Service> services, CancellationToken cancellationToken)
    {
        if (services == null || !services.Any())
            return new List<ServiceDto>();

        var allNodeIds = services.SelectMany(s => s.ScopeNodes.Select(sn => sn.StructureNodeId)).Distinct().ToList();
        var allNodes = allNodeIds.Any() ? await _structureNodeRepository.GetByIdsAsync(allNodeIds) : new List<StructureNode>();
        var nodeDict = allNodes.ToDictionary(n => n.Id);

        var result = new List<ServiceDto>();
        foreach (var service in services)
        {
            var serviceNodes = service.ScopeNodes
                .Select(sn => nodeDict.GetValueOrDefault(sn.StructureNodeId))
                .Where(n => n != null)
                .ToList();
            result.Add(await MapToDtoAsync(service, serviceNodes));
        }
        return result;
    }

    private Task<ServiceDto> MapToDtoAsync(Service service, List<StructureNode> nodes)
    {
        var scopeNodesDetails = nodes.Select(node => new ScopeNodeDetailsDto
        {
            Id = node.Id,
            Name = node.Name,
            LocalizedName = _localizationService.GetLocalizedString(node.Name)
        }).ToList();

        return Task.FromResult(new ServiceDto
        {
            Id = service.Id,
            Name = service.Name,
            Type = service.Type,
            Description = service.Description,
            IsActive = service.IsActive,
            IsPaid = service.IsPaid,
            Price = service.Price,
            ScopeNodeIds = service.ScopeNodes.Select(sn => sn.StructureNodeId).ToList(),
            ScopeNodesDetails = scopeNodesDetails,
            IncludeDescendants = service.IncludeDescendants,
            AcademicYearId = service.AcademicYearId,
            SemesterId = service.SemesterId,
            LevelOrder = service.LevelOrder,
            Workflow = service.Workflow != null ? MapWorkflowToDto(service.Workflow) : null
        });
    }

    private WorkflowDto MapWorkflowToDto(Workflow workflow)
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
                TransitionType = s.TransitionType,
                IsRequired = s.IsRequired,
                Fields = s.Fields.OrderBy(f => f.Order).Select(f => new WorkflowStepFieldDto
                {
                    Id = f.Id,
                    Order = f.Order,
                    Label = f.Label,
                    FieldType = f.FieldType,
                    IsRequired = f.IsRequired,
                    Options = f.OptionsJson != null ? JsonSerializer.Deserialize<List<string>>(f.OptionsJson) : null
                }).ToList()
            }).ToList()
        };
    }
}