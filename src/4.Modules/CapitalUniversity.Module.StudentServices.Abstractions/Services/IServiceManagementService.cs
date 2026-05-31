using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IServiceManagementService
{
    Task<IServiceDefinition> GetServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<IServiceDefinition>> GetAllActiveServicesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<IServiceDefinition>> GetAvailableServicesForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Guid> CreateServiceAsync(CreateServiceDto dto, CancellationToken cancellationToken = default);
    Task UpdateServiceAsync(Guid id, UpdateServiceDto dto, CancellationToken cancellationToken = default);
    Task DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleServiceStatusAsync(Guid id, CancellationToken cancellationToken = default);
}