using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Abstractions.Models;

public sealed class SyncPipelineRequest<TExternal, TInternal>
{
    public required SyncContext Context { get; init; }

    public required IDataExtractor<TExternal> Extractor { get; init; }

    public required IRecordMapper<TExternal, TInternal> Mapper { get; init; }

    public IRecordValidator<TInternal>? Validator { get; init; }

    public required IRecordWriter<TInternal> Writer { get; init; }

    /// <summary>
    /// Pure projection from an external record to the stable merge key.
    /// Used by the IdempotencyHandler to dedup records within a single run.
    /// </summary>
    public required Func<TExternal, string> ExternalKeySelector { get; init; }

    public int BatchSize { get; init; } = 500;

    public SyncCheckpoint? CurrentCheckpoint { get; init; }
}