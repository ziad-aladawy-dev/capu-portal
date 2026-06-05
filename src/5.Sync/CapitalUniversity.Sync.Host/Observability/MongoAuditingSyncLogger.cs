using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Infrastructure.Observability;

namespace CapitalUniversity.Sync.Host.Observability;

/// <summary>
/// Decorates the console/Serilog <see cref="SyncLogger"/> so sync <b>warnings and
/// errors</b> are additionally persisted to the Mongo audit trail. Info/Debug stay
/// console-only — the audit trail's job is the durable record of things that
/// changed (covered by the EF auto-trail) plus things that went wrong; routine
/// progress chatter would only bloat it.
///
/// <para>
/// The inner logger always runs first (console output is never lost). The Mongo
/// write is best-effort: <see cref="IAppLogger"/> is non-blocking (enqueue only)
/// and any failure is swallowed so observability never breaks a sync job.
/// </para>
/// </summary>
public sealed class MongoAuditingSyncLogger : ISyncLogger
{
    private const string AuditSource = "Sync";

    private readonly SyncLogger _inner;
    private readonly IAppLogger _audit;

    public MongoAuditingSyncLogger(SyncLogger inner, IAppLogger audit)
    {
        _inner = inner;
        _audit = audit;
    }

    public void LogDebug(Guid correlationId, string message, params object?[] args)
        => _inner.LogDebug(correlationId, message, args);

    public void LogInformation(Guid correlationId, string message, params object?[] args)
        => _inner.LogInformation(correlationId, message, args);

    public void LogWarning(Guid correlationId, string message, params object?[] args)
    {
        _inner.LogWarning(correlationId, message, args);
        TryAudit(() => _audit.LogWarningAsync(message, AuditSource, null, BuildMetadata(correlationId, args)));
    }

    public void LogError(Guid correlationId, Exception? exception, string message, params object?[] args)
    {
        _inner.LogError(correlationId, exception, message, args);
        // IAppLogger.LogErrorAsync requires a non-null exception; synthesise one
        // from the message when the caller didn't supply it so the entry still
        // records as an error rather than being dropped.
        var ex = exception ?? new InvalidOperationException(message);
        TryAudit(() => _audit.LogErrorAsync(message, ex, AuditSource, null, BuildMetadata(correlationId, args)));
    }

    public IDisposable BeginCorrelationScope(Guid correlationId)
        => _inner.BeginCorrelationScope(correlationId);

    private static Dictionary<string, object> BuildMetadata(Guid correlationId, object?[] args)
    {
        var metadata = new Dictionary<string, object>
        {
            [AuditMetadataKeys.Category] = LogCategory.Sync,
            ["CorrelationId"] = correlationId,
        };

        // The message is a structured template (named holes, not indices), so we
        // can't render it here without the formatter — keep the raw args alongside
        // for diagnosis.
        if (args is { Length: > 0 })
        {
            metadata["Args"] = args.Select(a => a?.ToString() ?? "null").ToArray();
        }

        return metadata;
    }

    private static void TryAudit(Func<Task> write)
    {
        try { _ = write(); }
        catch { /* observability must never break a sync job */ }
    }
}
