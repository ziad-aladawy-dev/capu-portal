using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// SyncOrphanReaperService sweeps stranded <c>Enqueued</c> runs to <c>Failed</c>.
/// These tests pin every branch: the Enabled gate, the cutoff = now-grace
/// computation pushed into the query, the empty-result short-circuit, the
/// per-orphan try/catch that keeps draining after one failure, and the
/// cancellation check — so conditional / arithmetic / loop mutations are killed.
/// Uses a real DI scope so <c>GetRequiredService</c> resolves the mocked repo.
/// </summary>
public class SyncOrphanReaperServiceTests
{
    private sealed class Ctx
    {
        public required SyncOrphanReaperService Sut { get; init; }
        public required Mock<ISyncRunRepository> Repo { get; init; }
        public required SyncOrphanReaperOptions Options { get; init; }
    }

    private static Ctx Build(Action<SyncOrphanReaperOptions>? configure = null)
    {
        var repo = new Mock<ISyncRunRepository>();
        var options = new SyncOrphanReaperOptions(); // Enabled=true, Grace=10, Max=1000
        configure?.Invoke(options);

        var monitor = new Mock<IOptionsMonitor<SyncOrphanReaperOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);

        var services = new ServiceCollection();
        services.AddSingleton(repo.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var sut = new SyncOrphanReaperService(scopeFactory, monitor.Object, new Mock<ISyncLogger>().Object);
        return new Ctx { Sut = sut, Repo = repo, Options = options };
    }

    private static SyncRunRecord Orphan(Guid id) => new()
    {
        CorrelationId = id,
        ModuleName = "Student",
        Direction = SyncDirection.Pull,
        TriggeredBy = "test",
        Queue = "default",
    };

    [Fact]
    public async Task Disabled_ShortCircuits_NoRepoCalls()
    {
        var c = Build(o => o.Enabled = false);

        await c.Sut.RunAsync();

        c.Repo.Verify(r => r.FindOrphanRunsAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        c.Repo.Verify(r => r.MarkFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoOrphans_DoesNotMarkAnything()
    {
        var c = Build();
        c.Repo.Setup(r => r.FindOrphanRunsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<SyncRunRecord>());

        await c.Sut.RunAsync();

        c.Repo.Verify(r => r.MarkFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryUsesCutoffNowMinusGraceAndMaxLimit()
    {
        var c = Build(o => { o.GraceMinutes = 30; o.MaxReapedPerRun = 250; });
        DateTimeOffset capturedCutoff = default;
        int capturedLimit = -1;
        c.Repo.Setup(r => r.FindOrphanRunsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .Callback<DateTimeOffset, int, CancellationToken>((cut, lim, _) => { capturedCutoff = cut; capturedLimit = lim; })
              .ReturnsAsync(Array.Empty<SyncRunRecord>());

        var before = DateTimeOffset.UtcNow.AddMinutes(-30);
        await c.Sut.RunAsync();
        var after = DateTimeOffset.UtcNow.AddMinutes(-30);

        capturedLimit.Should().Be(250);
        capturedCutoff.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task EligibleOrphans_AreMarkedFailedWithGraceInMessage()
    {
        var c = Build(o => o.GraceMinutes = 15);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        c.Repo.Setup(r => r.FindOrphanRunsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { Orphan(id1), Orphan(id2) });

        await c.Sut.RunAsync();

        c.Repo.Verify(r => r.MarkFailedAsync(id1, It.Is<string>(m => m.Contains("15 minutes")), It.IsAny<CancellationToken>()), Times.Once);
        c.Repo.Verify(r => r.MarkFailedAsync(id2, It.Is<string>(m => m.Contains("15 minutes")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OneMarkFailedThrows_OthersStillProcessed()
    {
        var c = Build();
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        c.Repo.Setup(r => r.FindOrphanRunsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { Orphan(bad), Orphan(good) });
        c.Repo.Setup(r => r.MarkFailedAsync(bad, It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("db down"));

        // The loop swallows the per-orphan failure and continues — no throw.
        await c.Sut.Invoking(s => s.RunAsync()).Should().NotThrowAsync();

        c.Repo.Verify(r => r.MarkFailedAsync(good, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancellation_StopsBeforeMarking()
    {
        var c = Build();
        c.Repo.Setup(r => r.FindOrphanRunsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { Orphan(Guid.NewGuid()) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await c.Sut.Invoking(s => s.RunAsync(cts.Token)).Should().ThrowAsync<OperationCanceledException>();

        c.Repo.Verify(r => r.MarkFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
