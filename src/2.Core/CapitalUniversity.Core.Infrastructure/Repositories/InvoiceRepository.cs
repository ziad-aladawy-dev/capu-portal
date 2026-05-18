using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Payments;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly CoreDbContext _context;

    public InvoiceRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<Invoice?> GetByIdAsync(Guid id, bool includeItems = true, bool includeTransactions = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices.AsQueryable();
        if (includeItems) query = query.Include(i => i.Items);
        if (includeTransactions) query = query.Include(i => i.Transactions);
        return query.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .AsNoTracking()
            .Where(i => i.StudentId == studentId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Invoice?> GetPendingForStudentAsync(Guid studentId, string currency, CancellationToken cancellationToken = default) =>
        _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(
                i => i.StudentId == studentId && i.Currency == currency && i.Status == InvoiceStatus.Pending,
                cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        await _context.Invoices.AddAsync(invoice, cancellationToken);

    public void Update(Invoice invoice) => _context.Invoices.Update(invoice);

    public Task<PaymentTransaction?> GetTransactionByKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.InvoiceId == invoiceId && t.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<PaymentTransaction>> GetTransactionsForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        await _context.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
}
