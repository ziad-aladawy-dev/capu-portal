using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Logging;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Logging;

/// <summary>
/// Buffered logger never blocks: it captures fields synchronously, enqueues, and
/// returns. The Mongo round-trip belongs to the background flush worker, not the
/// request thread.
/// </summary>
public class BufferedAppLoggerTests
{
    [Fact]
    public async Task LogInfoAsync_EnqueuesEntryAndReturnsImmediately()
    {
        var queue = new ChannelAuditLogQueue(capacity: 10);
        var sut = new BufferedAppLogger(queue);

        await sut.LogInfoAsync("hello", "TestSource", context: null,
            metadata: new Dictionary<string, object> { { "k", "v" } });

        queue.Count.Should().Be(1);

        var entry = await DrainOneAsync(queue);
        entry.Level.Should().Be(LogLevelType.Info);
        entry.Message.Should().Be("hello");
        entry.Source.Should().Be("TestSource");
        entry.Metadata.Should().Contain("k", "v");
        entry.ExceptionMessage.Should().BeNull();
    }

    [Fact]
    public async Task LogErrorAsync_CapturesExceptionDetails()
    {
        var queue = new ChannelAuditLogQueue();
        var sut = new BufferedAppLogger(queue);

        var ex = new InvalidOperationException("boom");
        await sut.LogErrorAsync("something failed", ex, "TestSource");

        var entry = await DrainOneAsync(queue);
        entry.Level.Should().Be(LogLevelType.Error);
        entry.ExceptionMessage.Should().Be("boom");
        entry.StackTrace.Should().BeNull("a never-thrown exception has no stack trace, just like the production path on a manually constructed Exception");
    }

    [Fact]
    public async Task EnqueueOverflow_SilentlyDrops()
    {
        var queue = new ChannelAuditLogQueue(capacity: 2);
        var sut = new BufferedAppLogger(queue);

        // Don't drain — fill the channel.
        await sut.LogInfoAsync("1", "x");
        await sut.LogInfoAsync("2", "x");
        // This one should be dropped, not throw, not block.
        await sut.LogInfoAsync("3", "x");

        queue.Count.Should().Be(2, "overflow drops the third entry per DropWrite policy");
    }

    private static async Task<LogEntry> DrainOneAsync(IAuditLogQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var entry in queue.ReadAllAsync(cts.Token))
        {
            return entry;
        }
        throw new InvalidOperationException("queue was empty within the timeout");
    }
}
