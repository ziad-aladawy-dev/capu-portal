using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Persistence.Repositories;

public sealed class SyncRunRepository : ISyncRunRepository
{
    private readonly SyncDbContext _db;
    private readonly ILogger<SyncRunRepository> _logger;

    public SyncRunRepository(SyncDbContext db, ILogger<SyncRunRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OpenRunAsync(SyncRunRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        // True idempotency. The executor's self-heal path calls this defensively
        // when the dispatcher may have already created the row — a naive Add+Save
        // would throw PK-violation, get caught upstream, and emit a misleading
        // Error+alert. The pre-check + DbUpdateException swallow converts a normal
        // self-heal into a silent no-op.
        var exists = await _db.Runs
            .AsNoTracking()
            .AnyAsync(r => r.CorrelationId == record.CorrelationId, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        var entity = new SyncRunEntity
        {
            CorrelationId = record.CorrelationId,
            ModuleName = record.ModuleName,
            Direction = record.Direction,
            TriggeredBy = record.TriggeredBy,
            Queue = record.Queue,
            Status = record.Status,
            EnqueuedAt = record.EnqueuedAt,
            AttemptCount = 0,
            HangfireJobId = null
        };

        _db.Runs.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent dispatcher + executor self-heal both raced past the
            // exists-check. Treat as success: the row exists, just not from us.
            _logger.LogInformation(ex,
                "OpenRunAsync ignored concurrent-create race. CorrelationId={CorrelationId}.",
                record.CorrelationId);
            _db.ChangeTracker.Clear();
        }
    }

    public async Task LinkHangfireJobAsync(
        Guid correlationId,
        string hangfireJobId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hangfireJobId))
        {
            throw new ArgumentException("Hangfire job id required.", nameof(hangfireJobId));
        }

        var run = await _db.Runs
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            _logger.LogWarning(
                "LinkHangfireJobAsync skipped: no run for CorrelationId={CorrelationId}.",
                correlationId);
            return;
        }

        if (run.HangfireJobId is null)
        {
            run.HangfireJobId = hangfireJobId;
        }
        else if (!string.Equals(run.HangfireJobId, hangfireJobId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "LinkHangfireJobAsync ignored duplicate link. CorrelationId={CorrelationId} Existing={Existing} Attempted={Attempted}.",
                correlationId, run.HangfireJobId, hangfireJobId);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkStartedAsync(
        Guid correlationId,
        int attempt,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var run = await _db.Runs
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            _logger.LogWarning(
                "MarkStartedAsync skipped: no run for CorrelationId={CorrelationId}.",
                correlationId);
            return;
        }

        switch (run.Status)
        {
            case SyncRunStatus.Enqueued:
                run.Status = SyncRunStatus.Running;
                run.AttemptCount = attempt;
                run.StartedAt = startedAt;
                break;

            case SyncRunStatus.Running:
                // Retry: stay Running, bump AttemptCount, refresh StartedAt only if not set.
                run.AttemptCount = Math.Max(run.AttemptCount, attempt);
                run.StartedAt ??= startedAt;
                break;

            case SyncRunStatus.DeadLettered:
                // Manual-requeue recovery: operator pushed the job back into the
                // queue via /admin/requeue. Re-open the audit window so the
                // intermediate attempt is visible in dashboards rather than the
                // audit appearing frozen on DeadLettered until completion.
                run.Status = SyncRunStatus.Running;
                run.AttemptCount = Math.Max(run.AttemptCount, attempt);
                run.StartedAt = startedAt;
                _logger.LogInformation(
                    "MarkStartedAsync recovering DeadLettered run. CorrelationId={CorrelationId} Attempt={Attempt}.",
                    correlationId, attempt);
                break;

            default:
                _logger.LogWarning(
                    "MarkStartedAsync ignored invalid transition. CorrelationId={CorrelationId} From={From} To={To}.",
                    correlationId, run.Status, SyncRunStatus.Running);
                return;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkSucceededAsync(
        Guid correlationId,
        int recordsProcessed,
        int recordsFailed,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var run = await _db.Runs
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            _logger.LogWarning(
                "MarkSucceededAsync skipped: no run for CorrelationId={CorrelationId}.",
                correlationId);
            return;
        }

        // Allow DeadLettered → Succeeded for the manual-requeue recovery path:
        // operator re-runs a dead-lettered job via /admin/requeue, it succeeds,
        // and the audit should reflect reality rather than freeze on DeadLettered.
        // Already-Succeeded runs aren't overwritten (idempotent no-op).
        if (run.Status is not (SyncRunStatus.Running or SyncRunStatus.DeadLettered))
        {
            _logger.LogWarning(
                "MarkSucceededAsync ignored invalid transition. CorrelationId={CorrelationId} From={From} To={To}.",
                correlationId, run.Status, SyncRunStatus.Succeeded);
            return;
        }

        if (run.Status == SyncRunStatus.DeadLettered)
        {
            _logger.LogInformation(
                "MarkSucceededAsync recovering DeadLettered run (likely manual requeue). CorrelationId={CorrelationId}.",
                correlationId);
        }

        run.Status = SyncRunStatus.Succeeded;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.RecordsProcessed = recordsProcessed;
        run.RecordsFailed = recordsFailed;
        run.DurationTicks = duration.Ticks;
        run.LastError = null;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid correlationId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var run = await _db.Runs
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            _logger.LogWarning(
                "MarkFailedAsync skipped: no run for CorrelationId={CorrelationId}.",
                correlationId);
            return;
        }

        if (run.Status is not (SyncRunStatus.Enqueued or SyncRunStatus.Running))
        {
            _logger.LogWarning(
                "MarkFailedAsync ignored invalid transition. CorrelationId={CorrelationId} From={From} To={To}.",
                correlationId, run.Status, SyncRunStatus.Failed);
            return;
        }

        run.Status = SyncRunStatus.Failed;
        run.LastError = TextHelpers.Truncate(errorMessage, 4000);
        run.CompletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }


    public async Task MarkCancelledAsync(
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var run = await _db.Runs
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            _logger.LogWarning(
                "MarkCancelledAsync skipped: no run for CorrelationId={CorrelationId}.",
                correlationId);
            return;
        }

        if (run.Status != SyncRunStatus.Running)
        {
            _logger.LogWarning(
                "MarkCancelledAsync ignored invalid transition. CorrelationId={CorrelationId} From={From} To={To}.",
                correlationId, run.Status, SyncRunStatus.Cancelled);
            return;
        }

        run.Status = SyncRunStatus.Cancelled;
        run.CompletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SyncRunRecord>> FindOrphanRunsAsync(CancellationToken cancellationToken)
    {
        var orphans = await _db.Runs
            .AsNoTracking()
            .Where(r => r.Status == SyncRunStatus.Enqueued && r.HangfireJobId == null)
            .OrderBy(r => r.EnqueuedAt)
            .Select(r => new
            {
                r.CorrelationId,
                r.ModuleName,
                r.Direction,
                r.TriggeredBy,
                r.Queue,
                r.EnqueuedAt,
                r.Status
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return orphans
            .Select(o => new SyncRunRecord
            {
                CorrelationId = o.CorrelationId,
                ModuleName = o.ModuleName,
                Direction = o.Direction,
                TriggeredBy = o.TriggeredBy,
                Queue = o.Queue,
                EnqueuedAt = o.EnqueuedAt,
                Status = o.Status
            })
            .ToList();
    }

}