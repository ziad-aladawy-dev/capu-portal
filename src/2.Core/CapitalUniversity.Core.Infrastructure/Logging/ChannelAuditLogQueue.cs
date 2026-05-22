using System.Threading.Channels;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Logging;

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Backing channel for the audit-log flush pipeline. Bounded with a configurable
/// capacity (default 10,000): producers fast-fail on overflow rather than block,
/// since blocking a request thread on log backpressure is the opposite of what
/// async logging is meant to achieve.
/// </summary>
public sealed class ChannelAuditLogQueue : IAuditLogQueue
{
    private readonly Channel<LogEntry> _channel;

    public ChannelAuditLogQueue(int capacity = 10_000)
    {
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(capacity)
        {
            // DropWrite: producer's TryWrite returns false on full; we silently swallow.
            // This is the right policy for audit logs — a log line is cheaper than a
            // wedged request.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public void Enqueue(LogEntry entry) => _channel.Writer.TryWrite(entry);

    public IAsyncEnumerable<LogEntry> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public int Count => _channel.Reader.Count;
}
