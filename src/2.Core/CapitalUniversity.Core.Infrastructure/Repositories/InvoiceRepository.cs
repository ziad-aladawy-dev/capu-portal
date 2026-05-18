using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Payments;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
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

    // Name of the unique index EF generates for HasIndex(new { x.InvoiceId, x.IdempotencyKey }).IsUnique()
    // on the PaymentTransactions table.
    private const string IdempotencyIndexName = "IX_PaymentTransactions_InvoiceId_IdempotencyKey";

    public async Task<(PaymentTransaction Saved, bool WasReplay)> SaveTransactionWithIdempotencyAsync(
        PaymentTransaction newTransaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (newTransaction, false);
        }
        catch (DbUpdateException ex) when (IsIdempotencyDuplicate(ex))
        {
            // Detach the row we tried to insert so the DbContext doesn't keep
            // trying to push it on subsequent saves in this scope.
            _context.Entry(newTransaction).State = EntityState.Detached;

            var winner = await _context.PaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.InvoiceId == newTransaction.InvoiceId
                         && t.IdempotencyKey == newTransaction.IdempotencyKey,
                    cancellationToken);

            // The unique violation guarantees the row exists; this is defensive.
            if (winner is null) throw;

            return (winner, true);
        }
    }

    private static bool IsIdempotencyDuplicate(DbUpdateException ex)
    {
        // SQL Server: 2627 = unique constraint violation, 2601 = unique index
        // violation. We narrow on both the error number AND the index name so
        // unrelated unique constraints elsewhere on the table never count as
        // an idempotency replay.
        if (ex.InnerException is not SqlException sql) return false;
        if (sql.Number != 2627 && sql.Number != 2601) return false;
        return sql.Message.Contains(IdempotencyIndexName, StringComparison.OrdinalIgnoreCase);
    }
}
