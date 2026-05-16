namespace CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;

/// <summary>
/// Writes outbox rows. Implementations stage the row on the current DbContext but
/// do NOT call SaveChanges — the caller is expected to commit the row alongside
/// the business state in a single transaction (that's the whole point of the
/// outbox pattern).
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Stage a message for asynchronous delivery. <paramref name="payload"/> is
    /// JSON-serialised; the matching <c>IOutboxMessageHandler</c> on the receiving
    /// side is responsible for its schema.
    /// </summary>
    System.Threading.Tasks.Task EnqueueAsync<TPayload>(
        string messageType,
        TPayload payload,
        System.Threading.CancellationToken cancellationToken = default);
}

/// <summary>
/// Processes a specific <c>MessageType</c> off the outbox. The dispatcher invokes
/// <see cref="HandleAsync"/> with the raw JSON payload — the handler deserialises
/// to its expected shape.
///
/// <para>
/// <b>Handlers must be idempotent.</b> The outbox guarantees at-least-once, not
/// exactly-once delivery: a process crash between handler success and the row
/// being marked processed will replay the handler on the next dispatcher tick.
/// </para>
/// </summary>
public interface IOutboxMessageHandler
{
    /// <summary>The discriminator this handler responds to. Must match the value
    /// passed to <see cref="IOutbox.EnqueueAsync"/>.</summary>
    string MessageType { get; }

    System.Threading.Tasks.Task HandleAsync(
        string payload,
        System.Threading.CancellationToken cancellationToken);
}

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Dispatcher polling interval. Default 5s — short enough to feel
    /// real-time in tests + dev, low enough overhead in prod.</summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>Max messages drained per poll cycle. Caps work-per-iteration so
    /// a backlog doesn't monopolise the dispatcher thread.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Maximum dispatch attempts before a message is considered poison
    /// and parked (left in the DB with <c>AttemptCount &gt;= MaxAttempts</c>).</summary>
    public int MaxAttempts { get; set; } = 5;
}
