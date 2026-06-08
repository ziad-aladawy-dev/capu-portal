using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

/// <summary>
/// EF-backed implementation of <see cref="IOutboxPoisonQueue"/>. Reads are
/// projected (no full-row materialisation) and ordered oldest-first so the
/// most-stuck rows surface first. Requeue resets <c>AttemptCount</c> + clears
/// the poison flag and persists in a single SaveChanges.
/// </summary>
public class OutboxPoisonQueue : IOutboxPoisonQueue
{
    private readonly CoreDbContext _dbContext;
    private readonly IEnumerable<IOutboxMessageHandler> _handlers;

    public OutboxPoisonQueue(CoreDbContext dbContext, IEnumerable<IOutboxMessageHandler> handlers)
    {
        _dbContext = dbContext;
        _handlers = handlers;
    }

    public async Task<IReadOnlyList<PoisonedOutboxEntry>> GetPoisonedAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);

        var rows = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.IsPoisoned && m.ProcessedAt == null)
            .OrderBy(m => m.PoisonedAt)
            .Take(safeLimit)
            .Select(m => new PoisonedOutboxEntry
            {
                Id = m.Id,
                MessageType = m.MessageType,
                Payload = m.Payload,
                EnqueuedAt = m.EnqueuedAt,
                PoisonedAt = m.PoisonedAt!.Value,
                AttemptCount = m.AttemptCount,
                LastError = m.LastError,
            })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (row is null || row.ProcessedAt is not null) return false;

        // M11 — refuse to requeue messages whose handler has been removed from
        // DI since the row was first written. Without this guard, the row goes
        // straight back into the "no handler registered" failure path on the
        // next dispatcher tick and ends up re-poisoned in a loop, masking the
        // real issue (handler renamed / module unregistered). The operator
        // sees false and gets the row id in the audit trail; they can decide
        // whether to drop the row or restore the missing handler.
        var hasHandler = _handlers.Any(h => string.Equals(h.MessageType, row.MessageType, StringComparison.Ordinal));
        if (!hasHandler) return false;

        if (_dbContext.Database.IsRelational())
        {
            // E1 — reset the poison flags with a guarded UPDATE so we never clobber
            // a row the dispatcher stamped ProcessedAt on between the read above and
            // this write. WHERE ProcessedAt IS NULL makes the requeue a no-op (and
            // returns false) when the row was processed in that window. Also clears
            // any stale lease so the row is immediately claimable again.
            var affected = await _dbContext.OutboxMessages
                .Where(m => m.Id == messageId && m.ProcessedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(m => m.AttemptCount, 0)
                        .SetProperty(m => m.IsPoisoned, false)
                        .SetProperty(m => m.PoisonedAt, (DateTime?)null)
                        .SetProperty(m => m.LastError, (string?)null)
                        .SetProperty(m => m.LockedBy, (Guid?)null)
                        .SetProperty(m => m.LockedUntil, (DateTime?)null),
                    cancellationToken);
            return affected > 0;
        }

        row.AttemptCount = 0;
        row.IsPoisoned = false;
        row.PoisonedAt = null;
        row.LastError = null;
        row.LockedBy = null;
        row.LockedUntil = null;
        // Force-mark modified so the row is persisted even when EF's auto
        // change-detection misses the property diffs (notably when the entity
        // was first written by a different DbContext, which the EF Core
        // InMemory provider mishandles).
        _dbContext.Entry(row).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
