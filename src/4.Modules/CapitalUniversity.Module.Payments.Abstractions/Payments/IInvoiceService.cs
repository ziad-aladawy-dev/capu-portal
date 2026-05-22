

using CapitalUniversity.Modules.Payments.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions;

public interface IInvoiceService
{
    Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken = default);
}
