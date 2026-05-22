

using CapitalUniversity.Modules.Payments.Domain;

namespace CapitalUniversity.Modules.Payments.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, bool includeItems = true, bool includeTransactions = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetPendingForStudentAsync(Guid studentId, string currency, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    void Update(Invoice invoice);

    Task<PaymentTransaction?> GetTransactionByKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentTransaction>> GetTransactionsForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the tracked changes, special-casing a unique-index violation on
    /// <c>(InvoiceId, IdempotencyKey)</c>: when a concurrent webhook wins the
    /// race, this returns the existing transaction and reports <c>WasReplay</c>
    /// = true. Any other <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> rethrows.
    /// </summary>
    Task<(PaymentTransaction Saved, bool WasReplay)> SaveTransactionWithIdempotencyAsync(
        PaymentTransaction newTransaction,
        CancellationToken cancellationToken = default);
}
