using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Schedules.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxExtractor : IDataExtractor<ScheduleSlotOutboxEntity>
{
    public const int MaxPerRun = 500;

    private readonly SchedulesSyncDbContext _db;

    public ScheduleSlotOutboxExtractor(SchedulesSyncDbContext db)
    {
        _db = db;
    }

    public async IAsyncEnumerable<ScheduleSlotOutboxEntity> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = await _db.ScheduleSlotsOutbox
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
