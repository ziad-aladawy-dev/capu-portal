using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface ISyncModule
{
    string ModuleName { get; }

    Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken);

    Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken);
}