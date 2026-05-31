using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Audit persistence for sync runs and their dispatched Hangfire jobs.
/// Hangfire still owns job state; these rows are operational audit only.
/// </summary>
public interface ISyncRunRepository
{
    Task OpenRunAsync(SyncRunRecord record, CancellationToken cancellationToken);

    Task LinkHangfireJobAsync(
        Guid correlationId,
        string hangfireJobId,
        CancellationToken cancellationToken);

    Task MarkStartedAsync(
        Guid correlationId,
        int attempt,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken);

    Task MarkSucceededAsync(
        Guid correlationId,
        int recordsProcessed,
        int recordsFailed,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid correlationId,
        string errorMessage,
        CancellationToken cancellationToken);

    // Dead-letter transitions are written directly by SyncDeadLetterFilter via
    // SyncDbContext on Hangfire's terminal FailedState — no repository hop.

    /// <summary>
    /// Records a cooperative cancellation. Allowed only from <see cref="SyncRunStatus.Running"/>.
    /// Does not append a failure row; does not trigger dead-letter handling.
    /// </summary>
    Task MarkCancelledAsync(
        Guid correlationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns runs that were opened but never linked to a Hangfire job —
    /// <c>Status == Enqueued AND HangfireJobId IS NULL</c>.
    /// These are the result of an enqueue failure or a crash mid-dispatch.
    /// Read-only; no automatic recovery is performed.
    /// </summary>
    Task<IReadOnlyList<SyncRunRecord>> FindOrphanRunsAsync(CancellationToken cancellationToken);
}