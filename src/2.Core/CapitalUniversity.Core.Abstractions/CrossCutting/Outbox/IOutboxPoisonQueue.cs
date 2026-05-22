namespace CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;

/// <summary>
/// Operator-facing view over outbox rows that exhausted their retry budget.
/// Separated from <see cref="IOutbox"/> so producers never accidentally use it
/// in the hot path; this is purely an inspection / requeue seam.
/// </summary>
public interface IOutboxPoisonQueue
{
    /// <summary>
    /// Snapshot of currently poisoned rows, oldest first. Bounded by
    /// <paramref name="limit"/> — the table is unbounded so callers must page.
    /// </summary>
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<PoisonedOutboxEntry>> GetPoisonedAsync(
        int limit = 100,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets <c>AttemptCount</c> + clears the poison flag so the dispatcher
    /// picks the row up again on its next tick. Returns true if the row was
    /// found and requeued, false otherwise (already processed, deleted, etc).
    /// </summary>
    System.Threading.Tasks.Task<bool> RequeueAsync(
        System.Guid messageId,
        System.Threading.CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-model row for the poison queue. Mirrors the persistence shape with
/// the fields an operator actually wants — payload included so the failure
/// can be diagnosed without a second DB hop.
/// </summary>
public sealed class PoisonedOutboxEntry
{
    public System.Guid Id { get; init; }
    public string MessageType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public System.DateTime EnqueuedAt { get; init; }
    public System.DateTime PoisonedAt { get; init; }
    public int AttemptCount { get; init; }
    public string? LastError { get; init; }
}
