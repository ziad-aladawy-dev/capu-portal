using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface ISyncCheckpointStore
{
    Task<SyncCheckpoint?> GetAsync(string moduleName, CancellationToken cancellationToken);

    Task SaveAsync(string moduleName, SyncCheckpoint checkpoint, CancellationToken cancellationToken);
}