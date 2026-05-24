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

        row.AttemptCount = 0;
        row.IsPoisoned = false;
        row.PoisonedAt = null;
        row.LastError = null;
        // Force-mark modified so the row is persisted even when EF's auto
        // change-detection misses the property diffs (notably when the entity
        // was first written by a different DbContext, which the EF Core
        // InMemory provider mishandles).
        _dbContext.Entry(row).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
