using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// Stamps every request with a correlation ID so a single user action can be
/// traced across logs, downstream calls, and the response. Caller-supplied IDs
/// are honoured when present (so a frontend or upstream gateway can pin a
/// session), otherwise a fresh GUID is minted.
///
/// <para>
/// The ID is exposed three ways:
///   <list type="bullet">
///     <item><c>HttpContext.Items["CorrelationId"]</c> — for downstream code in this request.</item>
///     <item>Response header <c>X-Correlation-Id</c> — for clients to log alongside their own traces.</item>
///     <item>Log scope key <c>CorrelationId</c> — so every <see cref="ILogger"/> entry inside the scope carries it.</item>
///   </list>
/// </para>
///
/// <para>
/// Sits before <c>RequestLoggingMiddleware</c> so the start/finish log lines
/// already inherit the scope.
/// </para>
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";
    private const int MaxAcceptedLength = 128;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveIncoming(context) ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            // Late stamp so any downstream rewrite of the response still emits the header.
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await _next(context);
        }
    }

    private static string? ResolveIncoming(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out StringValues values)) return null;
        var candidate = values.ToString();
        if (string.IsNullOrWhiteSpace(candidate)) return null;
        if (candidate.Length > MaxAcceptedLength) return null;
        // Anything not visibly safe (control chars, newlines that could split log lines)
        // is dropped — the response header echoes this value back to clients.
        foreach (var ch in candidate)
        {
            if (ch < 0x20 || ch == 0x7F) return null;
        }
        return candidate;
    }
}
