using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Admin CRUD over service → Treasury-receipt mappings. Deactivation is a soft
/// flip (<c>IsActive = false</c>), never a delete.
/// </summary>
public interface IServiceReceiptMappingService
{
    Task<Guid> CreateAsync(CreateServiceReceiptMappingRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceReceiptMappingResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ServiceReceiptMappingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
