using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Generic batch-oriented sync pipeline. Modules supply the per-domain pieces;
/// the pipeline owns batching, idempotency, mapping orchestration, and merge fan-out.
/// Checkpoint advancement is the module's responsibility after a successful run.
/// </summary>
public interface ISyncPipeline
{
    Task<SyncResult> RunAsync<TExternal, TInternal>(
        SyncPipelineRequest<TExternal, TInternal> request,
        CancellationToken cancellationToken);
}