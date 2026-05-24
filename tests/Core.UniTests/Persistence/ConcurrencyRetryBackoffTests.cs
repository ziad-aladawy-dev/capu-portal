using System.Reflection;
using CapitalUniversity.Core.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Persistence;

/// <summary>
/// H10 — verifies the jittered exponential backoff added to
/// <see cref="ConcurrencyRetry"/>. Catches regressions where someone removes
/// the delay to "speed up" a unit test, which would re-introduce the
/// CPU-pinning retry storm under contention.
/// </summary>
public class ConcurrencyRetryBackoffTests
{
    [Fact]
    public void NextDelay_GrowsExponentiallyWithAttempt_UpToCap()
    {
        // NextDelay is internal so we reflect once; testing the helper directly
        // is far more deterministic than racing real DbUpdateConcurrencyExceptions
        // and timing the await Task.Delay between them.
        var method = typeof(ConcurrencyRetry).GetMethod("NextDelay", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("NextDelay helper is missing — backoff was removed");

        // Sample over many trials to absorb jitter; the lower bound on each
        // attempt is deterministic (2^(n-1) * base, no jitter).
        const int trials = 50;
        var minByAttempt = new Dictionary<int, double>();
        var maxByAttempt = new Dictionary<int, double>();

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            double min = double.MaxValue;
            double max = 0;
            for (var t = 0; t < trials; t++)
            {
                var delay = (TimeSpan)method.Invoke(null, new object[] { attempt })!;
                min = Math.Min(min, delay.TotalMilliseconds);
                max = Math.Max(max, delay.TotalMilliseconds);
            }
            minByAttempt[attempt] = min;
            maxByAttempt[attempt] = max;
        }

        // Attempt 1 (exponent = 0) → base 20ms + jitter [0, 30) → range [20, 50).
        minByAttempt[1].Should().BeGreaterThanOrEqualTo(20);
        maxByAttempt[1].Should().BeLessThan(50);

        // Each subsequent attempt's minimum should double the previous min
        // (the jitter is bounded so the base-of-2 floor is the dominant
        // signal) until the 500ms cap kicks in.
        minByAttempt[2].Should().BeGreaterThanOrEqualTo(40);
        minByAttempt[3].Should().BeGreaterThanOrEqualTo(80);
        minByAttempt[4].Should().BeGreaterThanOrEqualTo(160);
        minByAttempt[5].Should().BeGreaterThanOrEqualTo(320);

        // 2^5 * 20 = 640ms which is over the 500ms cap, so attempt 6 must
        // be capped — verifies the MaxDelayMilliseconds floor.
        maxByAttempt[6].Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesAndAccumulatesObservableDelayBetweenAttempts()
    {
        var stamps = new List<DateTime>();

        await ConcurrencyRetry.ExecuteAsync(_ =>
        {
            stamps.Add(DateTime.UtcNow);
            if (stamps.Count < 3) throw new DbUpdateConcurrencyException();
            return Task.CompletedTask;
        });

        stamps.Should().HaveCount(3);
        // Each retry waits at least the un-jittered base for the current
        // attempt. Attempt 1→2 waits >= 20ms, attempt 2→3 waits >= 40ms,
        // so the total gap from first to last is >= ~60ms even at minimum
        // jitter. Generous lower bound (40ms) absorbs clock granularity.
        (stamps[2] - stamps[0]).TotalMilliseconds.Should().BeGreaterThan(40);
    }
}
