using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

public interface IServiceReceiptMappingRepository
{
    Task<ServiceReceiptMapping?> GetActiveByServiceAsync(Guid studentServiceId, CancellationToken cancellationToken = default);
    Task<ServiceReceiptMapping?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceReceiptMapping>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ServiceReceiptMapping mapping, CancellationToken cancellationToken = default);
    void Update(ServiceReceiptMapping mapping);
}
