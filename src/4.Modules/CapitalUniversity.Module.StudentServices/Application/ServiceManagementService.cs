using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

namespace CapitalUniversity.Module.StudentServices.Application;

public class ServiceManagementService : IServiceManagementService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IStudentRequestRepository _requestRepository;
    private readonly IUserScope _userScope;
    private readonly IRequestContext _requestContext;
    private readonly IStructureNodeRepository _structureNodeRepository;

    public ServiceManagementService(
        IServiceRepository serviceRepository,
        IStudentRequestRepository requestRepository,
        IUserScope userScope,
        IRequestContext requestContext,
        IStructureNodeRepository structureNodeRepository)
    {
        _serviceRepository = serviceRepository;
        _requestRepository = requestRepository;
        _userScope = userScope;
        _requestContext = requestContext;
        _structureNodeRepository = structureNodeRepository;
    }

    public async Task<IServiceDefinition> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdWithWorkflowAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");
        return new ServiceDefinitionAdapter(service);
    }

    public async Task<IEnumerable<IServiceDefinition>> GetAllActiveServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllActiveAsync(cancellationToken);
        return services.Select(s => new ServiceDefinitionAdapter(s)).ToList();
    }

    public async Task<IEnumerable<IServiceDefinition>> GetAvailableServicesForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        await _userScope.EnsureLoadedAsync(cancellationToken);
        var studentNodePath = _userScope.OwnStructureNodePath;
        var activeYearId = _requestContext.ActiveAcademicYearId;
        var activeSemesterId = _requestContext.ActiveSemesterId;
        var year = activeYearId?.ToString();
        var semester = activeSemesterId?.ToString();

        var services = await _serviceRepository.GetAvailableForStudentAsync(studentId, studentNodePath, year, semester, cancellationToken);
        return services.Select(s => new ServiceDefinitionAdapter(s)).ToList();
    }

    public async Task<Guid> CreateServiceAsync(CreateServiceDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.Scope.IsGlobalStructural && dto.Scope.StructureNodeId.HasValue)
        {
            var node = await _structureNodeRepository.GetByIdAsync(dto.Scope.StructureNodeId.Value);
            dto.Scope.StructureNodePath = node?.Path;
        }
        else
        {
            dto.Scope.StructureNodePath = null;
        }

        var service = new Service
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            IsPaid = dto.IsPaid,
            Price = dto.Price,
            Scope = dto.Scope,
            WorkflowId = dto.WorkflowId,
            FormFieldsJson = ValidateAndNormalizeFormFields(dto.FormFieldsJson)
        };

        await _serviceRepository.AddAsync(service, cancellationToken);
        await _serviceRepository.SaveChangesAsync(cancellationToken);
        return service.Id;
    }

    public async Task UpdateServiceAsync(Guid id, UpdateServiceDto dto, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");

        if (dto.Scope != null)
        {
            if (!dto.Scope.IsGlobalStructural && dto.Scope.StructureNodeId.HasValue)
            {
                var node = await _structureNodeRepository.GetByIdAsync(dto.Scope.StructureNodeId.Value);
                dto.Scope.StructureNodePath = node?.Path;
            }
            else
            {
                dto.Scope.StructureNodePath = null;
            }
            service.Scope = dto.Scope;
        }

        if (dto.Name != null) service.Name = dto.Name;
        if (dto.Description != null) service.Description = dto.Description;
        if (dto.IsPaid.HasValue) service.IsPaid = dto.IsPaid.Value;
        if (dto.Price.HasValue) service.Price = dto.Price;
        if (dto.Scope != null) service.Scope = dto.Scope;
        if (dto.WorkflowId.HasValue) service.WorkflowId = dto.WorkflowId.Value;
        if (dto.IsActive.HasValue) service.IsActive = dto.IsActive.Value;
        if (dto.FormFieldsJson != null) service.FormFieldsJson = ValidateAndNormalizeFormFields(dto.FormFieldsJson);

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

    private class ServiceDefinitionAdapter : IServiceDefinition
    {
        private readonly Service _service;
        public ServiceDefinitionAdapter(Service service) => _service = service;
        public Guid Id => _service.Id;
        public string Name => _service.Name;
        public string? Description => _service.Description;
        public bool IsActive => _service.IsActive;
        public bool IsPaid => _service.IsPaid;
        public decimal? Price => _service.Price;
        public ServiceScope Scope => _service.Scope;
        public IWorkflowDefinition Workflow => new WorkflowDefinitionAdapter(_service.Workflow);
        public string FormFieldsJson => _service.FormFieldsJson;
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

    private string ValidateAndNormalizeFormFields(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "[]";

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<object>>(json);
            return json;
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ValidationException("FormFieldsJson", "Invalid JSON format for form fields.");
        }
    }
}