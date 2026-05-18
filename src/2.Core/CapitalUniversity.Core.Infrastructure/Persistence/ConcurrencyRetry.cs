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
                // Loop. The action is expected to reload state on its next call.
            }
        }
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
