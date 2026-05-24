using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
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
        var query = _context.Set<Invoice>().AsQueryable();
        if (includeItems) query = query.Include(i => i.Items);
        if (includeTransactions) query = query.Include(i => i.Transactions);
        return query.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await _context.Set<Invoice>()
            .AsNoTracking()
            .Where(i => i.StudentId == studentId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Invoice?> GetPendingForStudentAsync(Guid studentId, string currency, CancellationToken cancellationToken = default) =>
        _context.Set<Invoice>()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(
                i => i.StudentId == studentId && i.Currency == currency && i.Status == InvoiceStatus.Pending,
                cancellationToken);

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        await _context.Set<Invoice>().AddAsync(invoice, cancellationToken);

    public void Update(Invoice invoice) => _context.Set<Invoice>().Update(invoice);

    private static readonly HashSet<string> InvoiceSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt", "dueAt", "amount", "status"
    };

    public async Task<PagedResult<Invoice>> SearchAsync(InvoiceSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Set<Invoice>().AsNoTracking().AsQueryable();

        if (query.StudentId.HasValue) q = q.Where(i => i.StudentId == query.StudentId.Value);
        if (query.Status.HasValue) q = q.Where(i => i.Status == query.Status.Value);

        // Date convention: From inclusive, To exclusive.
        if (query.IssuedFrom.HasValue) q = q.Where(i => i.CreatedAt >= query.IssuedFrom.Value);
        if (query.IssuedTo.HasValue) q = q.Where(i => i.CreatedAt < query.IssuedTo.Value);
        if (query.DueFrom.HasValue) q = q.Where(i => i.DueAt != null && i.DueAt >= query.DueFrom.Value);
        if (query.DueTo.HasValue) q = q.Where(i => i.DueAt != null && i.DueAt < query.DueTo.Value);

        if (query.MinAmount.HasValue) q = q.Where(i => i.TotalAmount >= query.MinAmount.Value);
        if (query.MaxAmount.HasValue) q = q.Where(i => i.TotalAmount <= query.MaxAmount.Value);

        if (!string.IsNullOrWhiteSpace(query.Currency)) q = q.Where(i => i.Currency == query.Currency);

        // Free-text search on the only string column the invoice owns. Item
        // descriptions are nested JSON — searching them would force per-row
        // deserialization. Left out by design.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(i => i.Currency.Contains(s));
        }

        var sortClauses = SortClause.Parse(query.Sort, InvoiceSortFields);
        q = ApplySort(q, sortClauses);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Invoice>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }

    private static IQueryable<Invoice> ApplySort(IQueryable<Invoice> q, IReadOnlyList<SortClause> sort)
    {
        if (sort.Count == 0) return q.OrderByDescending(i => i.CreatedAt);

        IOrderedQueryable<Invoice>? ordered = null;
        foreach (var clause in sort)
        {
            ordered = (clause.Field.ToLowerInvariant(), clause.Descending, ordered) switch
            {
                ("createdat", true,  null) => q.OrderByDescending(i => i.CreatedAt),
                ("createdat", false, null) => q.OrderBy(i => i.CreatedAt),
                ("dueat", true,  null) => q.OrderByDescending(i => i.DueAt),
                ("dueat", false, null) => q.OrderBy(i => i.DueAt),
                ("amount", true,  null) => q.OrderByDescending(i => i.TotalAmount),
                ("amount", false, null) => q.OrderBy(i => i.TotalAmount),
                ("status", true,  null) => q.OrderByDescending(i => i.Status),
                ("status", false, null) => q.OrderBy(i => i.Status),
                ("createdat", true,  _) => ordered!.ThenByDescending(i => i.CreatedAt),
                ("createdat", false, _) => ordered!.ThenBy(i => i.CreatedAt),
                ("dueat", true,  _) => ordered!.ThenByDescending(i => i.DueAt),
                ("dueat", false, _) => ordered!.ThenBy(i => i.DueAt),
                ("amount", true,  _) => ordered!.ThenByDescending(i => i.TotalAmount),
                ("amount", false, _) => ordered!.ThenBy(i => i.TotalAmount),
                ("status", true,  _) => ordered!.ThenByDescending(i => i.Status),
                ("status", false, _) => ordered!.ThenBy(i => i.Status),
                _ => ordered ?? q.OrderByDescending(i => i.CreatedAt),
            };
        }
        return ordered ?? q.OrderByDescending(i => i.CreatedAt);
    }

    public void ResetChangeTracker() => _context.ChangeTracker.Clear();

    public Task<PaymentTransaction?> GetTransactionByKeyAsync(Guid invoiceId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.Set<PaymentTransaction>()
            .FirstOrDefaultAsync(t => t.InvoiceId == invoiceId && t.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<PaymentTransaction>> GetTransactionsForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        await _context.Set<PaymentTransaction>()
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    private static readonly HashSet<string> TxSortFields = new(StringComparer.OrdinalIgnoreCase) { "createdAt", "amount", "status" };

    public async Task<PagedResult<PaymentTransaction>> SearchTransactionsAsync(PaymentTransactionSearchQuery query, CancellationToken cancellationToken = default)
    {
        var q = _context.Set<PaymentTransaction>().AsNoTracking().AsQueryable();

        if (query.InvoiceId.HasValue) q = q.Where(t => t.InvoiceId == query.InvoiceId.Value);
        if (query.Status.HasValue) q = q.Where(t => t.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Provider)) q = q.Where(t => t.Provider == query.Provider);
        if (query.From.HasValue) q = q.Where(t => t.CreatedAt >= query.From.Value);
        if (query.To.HasValue) q = q.Where(t => t.CreatedAt < query.To.Value);
        if (query.MinAmount.HasValue) q = q.Where(t => t.Amount >= query.MinAmount.Value);
        if (query.MaxAmount.HasValue) q = q.Where(t => t.Amount <= query.MaxAmount.Value);

        // StudentId requires a join to Invoice.
        if (query.StudentId.HasValue)
        {
            var sid = query.StudentId.Value;
            q = from t in q
                join i in _context.Set<Invoice>().AsNoTracking() on t.InvoiceId equals i.Id
                where i.StudentId == sid
                select t;
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.Provider.Contains(s) || t.ProviderTransactionId.Contains(s));
        }

        var sort = SortClause.Parse(query.Sort, TxSortFields);
        IOrderedQueryable<PaymentTransaction>? ord = null;
        if (sort.Count == 0)
        {
            ord = q.OrderByDescending(t => t.CreatedAt);
        }
        else
        {
            foreach (var c in sort)
            {
                ord = (c.Field.ToLowerInvariant(), c.Descending, ord) switch
                {
                    ("createdat", true, null) => q.OrderByDescending(t => t.CreatedAt),
                    ("createdat", false, null) => q.OrderBy(t => t.CreatedAt),
                    ("amount", true, null) => q.OrderByDescending(t => t.Amount),
                    ("amount", false, null) => q.OrderBy(t => t.Amount),
                    ("status", true, null) => q.OrderByDescending(t => t.Status),
                    ("status", false, null) => q.OrderBy(t => t.Status),
                    ("createdat", true, _) => ord!.ThenByDescending(t => t.CreatedAt),
                    ("createdat", false, _) => ord!.ThenBy(t => t.CreatedAt),
                    ("amount", true, _) => ord!.ThenByDescending(t => t.Amount),
                    ("amount", false, _) => ord!.ThenBy(t => t.Amount),
                    ("status", true, _) => ord!.ThenByDescending(t => t.Status),
                    ("status", false, _) => ord!.ThenBy(t => t.Status),
                    _ => ord,
                };
            }
        }

        q = ord ?? q.OrderByDescending(t => t.CreatedAt);

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PaymentTransaction>
        {
            Items = items,
            Page = query.NormalizedPage,
            PageSize = query.NormalizedPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.NormalizedPageSize),
        };
    }

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

            var winner = await _context.Set<PaymentTransaction>()
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
