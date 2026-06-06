namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface ISyncLogger
{
    void LogDebug(Guid correlationId, string message, params object?[] args);

    void LogInformation(Guid correlationId, string message, params object?[] args);

    void LogWarning(Guid correlationId, string message, params object?[] args);

    void LogError(Guid correlationId, Exception? exception, string message, params object?[] args);

    IDisposable BeginCorrelationScope(Guid correlationId);
}