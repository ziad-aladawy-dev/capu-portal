using CapitalUniversity.Core.Domain.Payments;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, bool includeItems = true, bool includeTransactions = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetPendingForStudentAsync(Guid studentId, string currency, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    void Update(Invoice invoice);

    Task<PaymentTransaction?> GetTransactionByKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTransaction>> GetTransactionsForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
