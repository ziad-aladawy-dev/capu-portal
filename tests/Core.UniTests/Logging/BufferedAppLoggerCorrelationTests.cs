using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Logging;

/// <summary>
/// Correlation-id propagation: the value written by
/// <c>CorrelationIdMiddleware</c> into <c>HttpContext.Items</c> must travel
/// with each <see cref="LogEntry"/> through the async queue and into the
/// flushed audit row. Without this, Mongo audit logs cannot be joined back
/// to the Serilog text logs that share the same id via the middleware's
/// log scope.
/// </summary>
public class BufferedAppLoggerCorrelationTests
{
    [Fact]
    public async Task LogInfoAsync_WithHttpContextCarryingCorrelationId_CapturesItOnEntry()
    {
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var context = new DefaultHttpContext();
        var correlationId = Guid.NewGuid().ToString("N");
        context.Items[CorrelationContext.ItemKey] = correlationId;

        await sut.LogInfoAsync("hello", "TestSource", context);

        var entry = await DrainOneAsync(queue);
        entry.CorrelationId.Should().Be(correlationId,
            "the value placed by CorrelationIdMiddleware must survive the synchronous capture in BufferedAppLogger.Build");
    }

    [Fact]
    public async Task CorrelationId_SurvivesAsyncFlushThroughQueue()
    {
        // This is the regression the audit called out: even after the
        // buffered logger returns and the worker drains async, the entry
        // hits Mongo carrying the request's correlation id. We assert this
        // by reading the entry off the same queue the flush worker reads
        // from — the worker just calls InsertOneAsync on what we get here.
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var ctx = new DefaultHttpContext();
        const string id = "abc-123-def-456";
        ctx.Items[CorrelationContext.ItemKey] = id;

        await sut.LogInfoAsync("req-1", "S", ctx);
        await sut.LogWarningAsync("req-1", "S", ctx);
        await sut.LogErrorAsync("req-1", new InvalidOperationException("x"), "S", ctx);

        var drained = await DrainAsync(queue, count: 3);
        drained.Should().HaveCount(3);
        drained.Should().OnlyContain(e => e.CorrelationId == id);
    }

    [Fact]
    public async Task LogInfoAsync_WithoutHttpContext_LeavesCorrelationIdNull()
    {
        // Background log call (no request). Must not throw, must not invent
        // an id — null is the explicit "no correlation available" marker.
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        await sut.LogInfoAsync("bg-tick", "BackgroundService", context: null);

        var entry = await DrainOneAsync(queue);
        entry.CorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task LogInfoAsync_WithHttpContextButNoCorrelationItem_LeavesCorrelationIdNull()
    {
        // Middleware didn't run (e.g. a synthetic context). Must not crash;
        // resolve must just return null. Catches a regression where someone
        // assumes the key is always present and dereferences blindly.
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var ctx = new DefaultHttpContext(); // no Items[ItemKey] set

        await sut.LogInfoAsync("orphan", "S", ctx);

        var entry = await DrainOneAsync(queue);
        entry.CorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task LogInfoAsync_WithMalformedCorrelationItem_DoesNotCrashAndReturnsNull()
    {
        // Defensive: an upstream that stored a non-string under the
        // correlation key (e.g. a Guid object) must not poison the log
        // pipeline. The resolver casts via `as string` and degrades to null.
        var queue = new ChannelAuditLogQueue(capacity: 4);
        var sut = new BufferedAppLogger(queue);

        var ctx = new DefaultHttpContext();
        ctx.Items[CorrelationContext.ItemKey] = Guid.NewGuid(); // wrong type on purpose

        var act = () => sut.LogInfoAsync("weird", "S", ctx);
        await act.Should().NotThrowAsync();

        var entry = await DrainOneAsync(queue);
        entry.CorrelationId.Should().BeNull();
    }

    private static async Task<LogEntry> DrainOneAsync(ChannelAuditLogQueue queue)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var entry in queue.ReadAllAsync(cts.Token))
        {
            return entry;
        }
        throw new InvalidOperationException("queue was empty within the timeout");
    }

    private static async Task<List<LogEntry>> DrainAsync(ChannelAuditLogQueue queue, int count)
    {
        var result = new List<LogEntry>(count);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var entry in queue.ReadAllAsync(cts.Token))
        {
            result.Add(entry);
            if (result.Count == count) return result;
        }
        return result;
    }
}
