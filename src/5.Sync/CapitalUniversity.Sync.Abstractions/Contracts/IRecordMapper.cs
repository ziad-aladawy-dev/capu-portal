namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Pure transformation from an external record to its internal representation.
/// Must be deterministic and side-effect free.
/// </summary>
public interface IRecordMapper<in TExternal, out TInternal>
{
    TInternal Map(TExternal external);
}