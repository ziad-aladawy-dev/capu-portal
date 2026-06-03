using CapitalUniversity.Module.StudentServices.Abstractions.Dto;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IServiceManagementService
{
    Task<ServiceDto> GetServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ServiceDto>> GetAllActiveServicesAsync(CancellationToken cancellationToken = default);
    Task<List<ServiceDto>> GetAvailableServicesForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Guid> CreateServiceAsync(CreateServiceDto dto, CancellationToken cancellationToken = default);
    Task UpdateServiceAsync(Guid id, UpdateServiceDto dto, CancellationToken cancellationToken = default);
    Task DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleServiceStatusAsync(Guid id, CancellationToken cancellationToken = default);
}