using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Finance.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Finance.Push;

public sealed class InvoiceOutboxExtractor : IDataExtractor<InvoiceOutboxEntity>
{
    public const int MaxPerRun = 500;

    private readonly FinanceSyncDbContext _db;

    public InvoiceOutboxExtractor(FinanceSyncDbContext db)
    {
        _db = db;
    }

    public async IAsyncEnumerable<InvoiceOutboxEntity> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = await _db.InvoicesOutbox
            .Where(r => r.Status == OutboxStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Take(MaxPerRun)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }
}
