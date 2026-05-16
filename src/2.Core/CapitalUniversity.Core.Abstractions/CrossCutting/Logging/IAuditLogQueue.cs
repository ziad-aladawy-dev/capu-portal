using System.Collections.Generic;
using System.Threading;
using CapitalUniversity.Core.Domain.Logging;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

/// <summary>
/// Producer/consumer seam between the request-path logger and the background
/// flush worker. Producers stage a <see cref="LogEntry"/> synchronously and return
/// immediately; the worker drains the queue and persists rows out-of-band, so a
/// slow log sink (Mongo network round-trip) never blocks the request thread.
///
/// <para>
/// Implementations are unbounded by default but may be bounded — the producer must
/// be allowed to silently drop on overflow rather than block, because losing a log
/// row is preferable to wedging the request pipeline.
/// </para>
/// </summary>
public interface IAuditLogQueue
{
    /// <summary>Stage one entry. Non-blocking. Silently drops if the queue is full.</summary>
    void Enqueue(LogEntry entry);

    /// <summary>Drains every staged entry — yields until <paramref name="cancellationToken"/> is cancelled.</summary>
    IAsyncEnumerable<LogEntry> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Current depth — for tests / metrics.</summary>
    int Count { get; }
}
