using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

public interface IFailureRepository
{
    Task RecordAsync(SyncFailureRecord record, CancellationToken cancellationToken);
}