using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Infrastructure.Observability;

public sealed class SyncLogger : ISyncLogger
{
    public const string CorrelationIdKey = "SyncCorrelationId";

    private readonly ILogger<SyncLogger> _logger;

    public SyncLogger(ILogger<SyncLogger> logger)
    {
        _logger = logger;
    }

    public void LogDebug(Guid correlationId, string message, params object?[] args)
    {
        using var _ = BeginCorrelationScope(correlationId);
#pragma warning disable CA2254
        _logger.LogDebug(message, args);
#pragma warning restore CA2254
    }

    public void LogInformation(Guid correlationId, string message, params object?[] args)
    {
        using var _ = BeginCorrelationScope(correlationId);
#pragma warning disable CA2254
        _logger.LogInformation(message, args);
#pragma warning restore CA2254
    }

    public void LogWarning(Guid correlationId, string message, params object?[] args)
    {
        using var _ = BeginCorrelationScope(correlationId);
#pragma warning disable CA2254
        _logger.LogWarning(message, args);
#pragma warning restore CA2254
    }

    public void LogError(Guid correlationId, Exception? exception, string message, params object?[] args)
    {
        using var _ = BeginCorrelationScope(correlationId);
#pragma warning disable CA2254
        _logger.LogError(exception, message, args);
#pragma warning restore CA2254
    }

    public IDisposable BeginCorrelationScope(Guid correlationId)
    {
        var state = new Dictionary<string, object>
        {
            [CorrelationIdKey] = correlationId
        };
        return _logger.BeginScope(state) ?? NullScope.Instance;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}