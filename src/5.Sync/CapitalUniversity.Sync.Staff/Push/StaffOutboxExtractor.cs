using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Staff.Push;

public sealed class StaffOutboxExtractor : IDataExtractor<StaffOutboxEntity>
{
    public const int MaxPerRun = 500;

    private readonly StaffSyncDbContext _db;

    public StaffOutboxExtractor(StaffSyncDbContext db)
    {
        _db = db;
    }

    public async IAsyncEnumerable<StaffOutboxEntity> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = await _db.StaffOutbox
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