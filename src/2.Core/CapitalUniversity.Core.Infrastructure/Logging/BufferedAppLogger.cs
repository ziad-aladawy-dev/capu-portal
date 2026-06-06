using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Logging;
using Microsoft.AspNetCore.Http;
// CorrelationContext lives in CapitalUniversity.Core.Abstractions.CrossCutting.Logging
// (same namespace as IAppLogger above — no extra using needed).

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Non-blocking <see cref="IAppLogger"/>: captures HttpContext fields synchronously,
/// stages a <see cref="LogEntry"/> on <see cref="IAuditLogQueue"/>, and returns to
/// the caller. The <c>AuditLogFlushWorker</c> drains the queue and writes to Mongo
/// out of band — taking the audit-write latency off the request path.
///
/// <para>
/// Backpressure: the queue is bounded with <c>DropWrite</c>; under sustained
/// overflow log lines are silently dropped rather than blocking the producer. The
/// queue's <see cref="IAuditLogQueue.Count"/> is the metric to watch.
/// </para>
/// </summary>
public class BufferedAppLogger : IAppLogger
{
    private readonly IAuditLogQueue _queue;

    public BufferedAppLogger(IAuditLogQueue queue)
    {
        _queue = queue;
    }

    public Task LogInfoAsync(string message, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        _queue.Enqueue(Build(LogLevelType.Info, message, source, exception: null, context, metadata));
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        _queue.Enqueue(Build(LogLevelType.Warning, message, source, exception: null, context, metadata));
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message, Exception exception, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        _queue.Enqueue(Build(LogLevelType.Error, message, source, exception, context, metadata));
        return Task.CompletedTask;
    }

    public Task LogCriticalAsync(string message, Exception exception, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        _queue.Enqueue(Build(LogLevelType.Critical, message, source, exception, context, metadata));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Snapshot HttpContext fields synchronously — the worker dequeues asynchronously
    /// by which point the context may be disposed, so anything we want to log must
    /// be captured here on the request thread. Every free-form string is passed
    /// through <see cref="LogScrubber"/> before it lands on the entry — defense in
    /// depth against an upstream caller accidentally logging a token / password.
    /// </summary>
    private static LogEntry Build(
        LogLevelType level,
        string message,
        string source,
        Exception? exception,
        HttpContext? context,
        Dictionary<string, object>? metadata)
    {
        // Pull the reserved audit keys off BEFORE scrubbing so they land on
        // dedicated columns (and never reach the persisted metadata blob).
        var category = ExtractCategory(metadata, level);
        var action = ExtractString(metadata, AuditMetadataKeys.Action);
        var entityName = ExtractString(metadata, AuditMetadataKeys.Entity);

        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Level = level,
            Category = category,
            Action = action,
            EntityName = entityName,
            Message = LogScrubber.ScrubMessage(message) ?? string.Empty,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow,
            Metadata = LogScrubber.ScrubMetadata(metadata),
        };

        if (exception is not null)
        {
            entry.ExceptionMessage = LogScrubber.ScrubMessage(exception.Message);
            entry.StackTrace = LogScrubber.ScrubMessage(exception.StackTrace);
        }

        if (context is not null)
        {
            entry.IpAddress = ResolveClientIp(context);
            entry.RequestPath = context.Request.Path;
            entry.HttpMethod = context.Request.Method;
            entry.UserId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            entry.UserName = context.User?.FindFirstValue(ClaimTypes.Name);
            entry.Role = context.User?.FindFirstValue(ClaimTypes.Role);
            entry.CorrelationId = ResolveCorrelationId(context);
        }

        return entry;
    }

    /// <summary>
    /// Reads (and removes) the reserved category key. Falls back to a severity-
    /// derived default so uncategorised warnings/errors land in
    /// <see cref="LogCategory.Error"/> rather than silently as Data.
    /// </summary>
    private static LogCategory ExtractCategory(Dictionary<string, object>? metadata, LogLevelType level)
    {
        if (metadata is not null && metadata.TryGetValue(AuditMetadataKeys.Category, out var raw))
        {
            metadata.Remove(AuditMetadataKeys.Category);
            switch (raw)
            {
                case LogCategory c: return c;
                case string s when Enum.TryParse<LogCategory>(s, ignoreCase: true, out var parsed): return parsed;
            }
        }

        return level == LogLevelType.Info ? LogCategory.Data : LogCategory.Error;
    }

    private static string? ExtractString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var raw)) return null;
        metadata.Remove(key);
        return raw?.ToString();
    }

    /// <summary>
    /// Snapshots the correlation id set by <c>CorrelationIdMiddleware</c> so
    /// it travels with the entry through the async queue → Mongo flush. Null
    /// when the middleware didn't run (background work) or when the stored
    /// value isn't a string we can serialize — never throws, so a malformed
    /// upstream injection can't take down the log pipeline.
    /// </summary>
    private static string? ResolveCorrelationId(HttpContext context)
    {
        if (!context.Items.TryGetValue(CorrelationContext.ItemKey, out var raw)) return null;
        return raw as string;
    }

    private static string? ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var first = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            var v = realIp.ToString().Trim();
            if (!string.IsNullOrEmpty(v)) return v;
        }

        var remote = context.Connection.RemoteIpAddress?.ToString();
        return remote == "::1" ? "127.0.0.1" : remote;
    }
}
