using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Courses.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Courses.Push;

public sealed class CourseOutboxExtractor : IDataExtractor<CourseOutboxEntity>
{
    public const int MaxPerRun = 500;

    private readonly CoursesSyncDbContext _db;

    public CourseOutboxExtractor(CoursesSyncDbContext db)
    {
        _db = db;
    }

    public async IAsyncEnumerable<CourseOutboxEntity> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = await _db.CoursesOutbox
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
