namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncExecutionException : SyncException
{
    public SyncExecutionException(string message, Exception innerException, Guid correlationId, string moduleName)
        : base(message, innerException, correlationId, moduleName)
    {
    }

    public SyncExecutionException(string message, Guid correlationId, string moduleName)
        : base(message, correlationId, moduleName)
    {
    }
}