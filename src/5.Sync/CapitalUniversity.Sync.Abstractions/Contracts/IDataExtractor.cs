using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Module-supplied source. Streams external records honoring the supplied checkpoint.
/// Implementations are responsible for translating the checkpoint into a server-side filter
/// (UpdatedAt &gt; cursor, RowVersion, CDC range, etc.).
/// </summary>
public interface IDataExtractor<TExternal>
{
    IAsyncEnumerable<TExternal> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        CancellationToken cancellationToken);
}