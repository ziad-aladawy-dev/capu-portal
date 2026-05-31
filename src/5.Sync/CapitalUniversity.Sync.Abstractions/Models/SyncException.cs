namespace CapitalUniversity.Sync.Abstractions.Models;

public class SyncException : Exception
{
    public Guid? CorrelationId { get; }

    public string? ModuleName { get; }

    public SyncException(string message, Guid? correlationId = null, string? moduleName = null)
        : base(message)
    {
        CorrelationId = correlationId;
        ModuleName = moduleName;
    }

    public SyncException(string message, Exception innerException, Guid? correlationId = null, string? moduleName = null)
        : base(message, innerException)
    {
        CorrelationId = correlationId;
        ModuleName = moduleName;
    }
}