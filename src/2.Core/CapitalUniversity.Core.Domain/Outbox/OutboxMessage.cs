using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Outbox;

/// <summary>
/// Transactional-outbox row. Written in the same DbContext SaveChanges as the
/// business state it accompanies, then drained asynchronously by the dispatcher
/// hosted service.
///
/// <para>
/// Guarantees: at-least-once delivery (a handler may run more than once on
/// retry, so handlers must be idempotent — see <c>IOutboxMessageHandler</c>).
/// The combination of the business state + outbox row landing in a single
/// transaction means a successful commit always produces a deliverable
/// message, and a rollback always avoids one. Either both happen or neither.
/// </para>
/// </summary>
public class OutboxMessage : BaseEntity
{
    /// <summary>Handler discriminator. Must match an <c>IOutboxMessageHandler.MessageType</c>.</summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>JSON-serialised payload. The handler is responsible for its schema.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Stamped at enqueue time, in UTC.</summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the handler returns successfully. Null while pending.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Bumped on every dispatch attempt — caps retries via the dispatcher's policy.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Captured for diagnostics on the most recent failure.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// True once <see cref="AttemptCount"/> has hit the configured cap without
    /// success — the row is parked in the table for inspection rather than
    /// silently consuming retry slots. <see cref="PoisonedAt"/> stamps when.
    /// </summary>
    public bool IsPoisoned { get; set; }

    /// <summary>UTC instant the row was first marked poisoned.</summary>
    public DateTime? PoisonedAt { get; set; }

    /// <summary>
    /// Snapshot of the request's correlation id. Restored by the dispatcher
    /// so logs emitted by handlers are joinable to the originating request.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Snapshot of the request's culture (e.g. "ar", "en"). Restored by the
    /// dispatcher so handlers produce localized outputs (logs, notifications)
    /// consistent with the user's intent.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Lease token (C1). A dispatcher instance stamps its own <see cref="Guid"/>
    /// here when it atomically claims the row, then loads back only the rows
    /// carrying its token — so two dispatchers scaling horizontally can never
    /// both process the same message. Null while unclaimed.
    /// </summary>
    public Guid? LockedBy { get; set; }

    /// <summary>
    /// UTC instant the current lease expires. A row is claimable again once
    /// this passes, which prevents a crashed dispatcher from parking a row
    /// forever. Null while unclaimed.
    /// </summary>
    public DateTime? LockedUntil { get; set; }
}
