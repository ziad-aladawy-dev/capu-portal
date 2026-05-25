using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence;

/// <summary>
/// Opt-in helper for the P0.8 / P1.4 concurrency retry pattern. Callers wrap
/// a unit of work and the helper retries it on
/// <see cref="DbUpdateConcurrencyException"/> up to <c>maxAttempts</c>. The
/// caller's lambda is responsible for re-reading the entity inside the
/// closure — re-running the lambda re-runs the load.
///
/// <para>
/// Retry is OPT-IN per the agreed amendment to P0.8: only operations whose
/// outcome is independent of which writer wins (status transitions, balance
/// recalcs after a webhook) should use this. User-driven edits should let
/// the conflict surface as a 409 instead.
/// </para>
/// </summary>
public static class ConcurrencyRetry
{
    public const int DefaultMaxAttempts = 3;

    // H10 — jittered exponential backoff between attempts. Caps prevent
    // runaway delays from compounding when the helper is composed with the
    // EF Core SQL provider's transient-error retry policy on top.
    internal const int BaseDelayMilliseconds = 20;
    internal const int MaxJitterMilliseconds = 30;
    internal const int MaxDelayMilliseconds = 500;

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await action(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // H10 — exponential backoff with jitter so concurrent retries
                // don't dogpile the same row. Capped at MaxDelayMilliseconds so
                // we don't trade a CPU storm for an unbounded latency tail.
                await Task.Delay(NextDelay(attempt), cancellationToken);
            }
        }
    }

    internal static TimeSpan NextDelay(int attempt)
    {
        // attempt is 1-based after the throw, so the first wait uses 2^0.
        var exponent = Math.Max(0, attempt - 1);
        var baseMs = BaseDelayMilliseconds * Math.Pow(2, exponent);
        var jitter = Random.Shared.Next(0, MaxJitterMilliseconds);
        var capped = Math.Min(MaxDelayMilliseconds, baseMs + jitter);
        return TimeSpan.FromMilliseconds(capped);
    }

    public static Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        int maxAttempts = DefaultMaxAttempts,
        CancellationToken cancellationToken = default)
        => ExecuteAsync<object?>(async ct =>
        {
            await action(ct);
            return null;
        }, maxAttempts, cancellationToken);
}
