namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncModuleNotFoundException : SyncException
{
    public SyncModuleNotFoundException(string moduleName, Guid? correlationId = null)
        : base($"Sync module '{moduleName}' is not registered.", correlationId, moduleName)
    {
    }
}