using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface ISyncDispatcher
{
    Task<string> DispatchAsync(
        string moduleName,
        SyncDirection direction,
        SyncRunMetadata metadata,
        CancellationToken cancellationToken);
}