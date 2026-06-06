namespace CapitalUniversity.Sync.Infrastructure.Pipeline;

/// <summary>
/// In-memory per-run dedup of external keys. A fresh instance is created per
/// pipeline invocation, so state is bounded to a single run.
///
/// Combined with the module writer's external-key upsert semantics, this gives
/// the spec's idempotency guarantee: duplicate execution of the same run does
/// not produce duplicate writes.
/// </summary>
public sealed class IdempotencyHandler<TKey> where TKey : notnull
{
    private readonly HashSet<TKey> _seen;

    public IdempotencyHandler(IEqualityComparer<TKey>? comparer = null)
    {
        _seen = new HashSet<TKey>(comparer ?? EqualityComparer<TKey>.Default);
    }

    public bool TryAdd(TKey key) => _seen.Add(key);

    public int SeenCount => _seen.Count;
}