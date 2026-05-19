using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Persistence;

public class ConcurrencyRetryTests
{
    [Fact]
    public async Task ExecuteAsync_Generic_FirstAttemptSucceeds_ReturnsResult()
    {
        var calls = 0;

        var result = await ConcurrencyRetry.ExecuteAsync<int>(_ =>
        {
            calls++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_RetriesOnConcurrencyException_UntilSuccess()
    {
        var calls = 0;

        var result = await ConcurrencyRetry.ExecuteAsync<string>(_ =>
        {
            calls++;
            if (calls < 3) throw new DbUpdateConcurrencyException();
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_ExhaustsAttempts_Rethrows()
    {
        var calls = 0;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            ConcurrencyRetry.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new DbUpdateConcurrencyException();
            }, maxAttempts: 2));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_NonConcurrencyException_DoesNotRetry()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConcurrencyRetry.ExecuteAsync<int>(_ =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Generic_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;

        await ConcurrencyRetry.ExecuteAsync<int>(ct =>
        {
            seen = ct;
            return Task.FromResult(1);
        }, cancellationToken: cts.Token);

        Assert.Equal(cts.Token, seen);
    }

    [Fact]
    public async Task ExecuteAsync_Void_FirstAttemptSucceeds()
    {
        var calls = 0;

        await ConcurrencyRetry.ExecuteAsync(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Void_RetriesOnConcurrencyException()
    {
        var calls = 0;

        await ConcurrencyRetry.ExecuteAsync(_ =>
        {
            calls++;
            if (calls < 2) throw new DbUpdateConcurrencyException();
            return Task.CompletedTask;
        });

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Void_ExhaustsAttempts_Rethrows()
    {
        var calls = 0;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            ConcurrencyRetry.ExecuteAsync(_ =>
            {
                calls++;
                throw new DbUpdateConcurrencyException();
            }, maxAttempts: 2));

        Assert.Equal(2, calls);
    }
}
