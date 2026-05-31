using System.Runtime.CompilerServices;

namespace CapitalUniversity.Sync.Infrastructure.Pipeline;

/// <summary>
/// Streams an async source into bounded-memory batches. Last batch may be partial.
/// </summary>
public static class BatchProcessor
{
    public static async IAsyncEnumerable<IReadOnlyList<T>> ChunkAsync<T>(
        IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be >= 1.");
        }

        var buffer = new List<T>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            buffer.Add(item);
            if (buffer.Count >= batchSize)
            {
                yield return buffer;
                buffer = new List<T>(batchSize);
            }
        }

        if (buffer.Count > 0)
        {
            yield return buffer;
        }
    }
}