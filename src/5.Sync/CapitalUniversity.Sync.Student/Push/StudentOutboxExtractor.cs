using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Streams <see cref="OutboxStatus.Pending"/> outbox rows in CreatedAt order
/// for the push pipeline. Page-bounded by <see cref="MaxPerRun"/> so a single tick
/// never pulls a runaway backlog into memory — anything beyond the cap waits for the
/// next recurring tick. Rows are EF-tracked because the push writer mutates them
/// (Status/ProcessedAt/AttemptCount/LastError) inside the same DbContext.
///
/// <para>
/// The push flow does not use <see cref="SyncCheckpoint"/> for cursor tracking; the
/// outbox row's <c>Status</c> is the cursor. The checkpoint parameter is accepted
/// to satisfy <see cref="IDataExtractor{TExternal}"/> and intentionally ignored.
/// </para>
/// </summary>
public sealed class StudentOutboxExtractor : IDataExtractor<StudentOutboxEntity>
{
    /// <summary>
    /// Per-run cap on outbox rows materialized. A backlog larger than this is split
    /// across recurring ticks; this is defense-in-depth on top of the pipeline's
    /// batch-size guard so the EF tracker never grows beyond a predictable envelope.
    /// </summary>
    public const int MaxPerRun = 500;

    private readonly StudentSyncDbContext _db;

    public StudentOutboxExtractor(StudentSyncDbContext db)
    {
        _db = db;
    }

    public async IAsyncEnumerable<StudentOutboxEntity> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Materialize once — the writer iterates the same rows mutating their state.
        // AsAsyncEnumerable would interfere with subsequent SaveChangesAsync writes.
        var rows = await _db.StudentOutbox
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