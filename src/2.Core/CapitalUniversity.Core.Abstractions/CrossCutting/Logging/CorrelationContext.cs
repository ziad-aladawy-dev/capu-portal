namespace CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

/// <summary>
/// Single source of truth for the correlation-id key names that flow across
/// the request pipeline (middleware ⇄ HttpContext.Items) and the audit log
/// payload (<c>LogEntry.CorrelationId</c>).
///
/// <para>
/// The middleware writes <see cref="ItemKey"/> into <c>HttpContext.Items</c>
/// and stamps <see cref="HeaderName"/> onto the response. The buffered logger
/// reads the same <see cref="ItemKey"/> when it snapshots an entry on the
/// request thread, so the value persists through async flush to Mongo.
/// </para>
///
/// <para>
/// Strings live here (not at the call sites) so a future rename does not
/// silently desync the middleware from the logger — a desync would mean
/// audit entries without correlation ids despite the request having one.
/// </para>
/// </summary>
public static class CorrelationContext
{
    /// <summary>Key under which the middleware stores the correlation id in <c>HttpContext.Items</c>.</summary>
    public const string ItemKey = "CorrelationId";

    /// <summary>HTTP header name carrying the correlation id on inbound requests and outbound responses.</summary>
    public const string HeaderName = "X-Correlation-Id";
}
