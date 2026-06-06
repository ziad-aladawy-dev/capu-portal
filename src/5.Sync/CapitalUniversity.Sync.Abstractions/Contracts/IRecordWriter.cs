namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Module-supplied upsert sink. Returns the number of records persisted by this call
/// (insert + update).
///
/// <para>
/// <b>Implementations MUST be idempotent on the external merge key.</b> The sync runtime
/// reserves the right to replay an already-committed batch when:
/// <list type="bullet">
///   <item>Hangfire retries an attempt after a transient failure in a later batch.</item>
///   <item>The checkpoint save fails after a successful pipeline (run is replayed next tick).</item>
///   <item>The host crashes between commit and checkpoint advance.</item>
/// </list>
/// Writers must treat a re-presented record as an update of the existing internal row,
/// keyed by its stable external identifier. Duplicate logical rows are never acceptable.
/// </para>
///
/// <para>
/// Concurrency: writers should be prepared for the unique-constraint race where two
/// workers both observed "row does not exist" and both attempted an insert. The
/// recommended pattern is to catch the resulting <c>DbUpdateException</c> and converge
/// via re-read + retry once, NOT to silently swallow it.
/// </para>
/// </summary>
public interface IRecordWriter<in TInternal>
{
    Task<int> UpsertBatchAsync(
        IReadOnlyList<TInternal> batch,
        CancellationToken cancellationToken);
}