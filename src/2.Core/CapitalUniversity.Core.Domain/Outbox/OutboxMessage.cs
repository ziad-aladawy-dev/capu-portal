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
}
