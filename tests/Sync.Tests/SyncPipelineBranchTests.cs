using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Branch-complete coverage for <see cref="SyncPipeline"/> beyond the three
/// happy-path tests in <c>SyncPipelineTests</c>. Targets every decision the
/// orchestrator makes: the batch-size guard, optional validation + warning
/// roll-up, writer-skip accounting (partial / zero-progress / strict), the
/// idempotency hard cap, replay detection, per-batch writer retry, the three
/// exception arms (genuine-cancel rethrow, spurious-OCE, generic failure +
/// alert), and the fire-and-forget alert hook's swallow-on-throw contract.
/// </summary>
public class SyncPipelineBranchTests
{
    // ---------------- harness ----------------

    private static SyncContext Context(int attempt = 1, string module = "test") => new()
    {
        ModuleName = module,
        Direction = SyncDirection.Pull,
        Attempt = attempt,
        Metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "Test",
            Tags = new Dictionary<string, string>(),
        },
    };

    private static SyncOptions OptionsWith(Action<SyncPipelineOptions>? configure = null)
    {
        var o = new SyncOptions();
        configure?.Invoke(o.Pipeline);
        return o;
    }

    private static (SyncPipeline Sut, Mock<ISyncLogger> Logger, Mock<ISyncAlertingHook> Alert) BuildSut(
        SyncOptions? options = null, bool withAlert = true)
    {
        var logger = new Mock<ISyncLogger>();
        var monitor = new Mock<IOptionsMonitor<SyncOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options ?? new SyncOptions());
        var alert = new Mock<ISyncAlertingHook>();
        var sut = new SyncPipeline(logger.Object, monitor.Object, withAlert ? alert.Object : null);
        return (sut, logger, alert);
    }

    private static IDataExtractor<string> Extractor(IEnumerable<string> records)
    {
        var m = new Mock<IDataExtractor<string>>();
        m.Setup(x => x.ExtractAsync(It.IsAny<SyncContext>(), It.IsAny<SyncCheckpoint>(), It.IsAny<CancellationToken>()))
         .Returns(records.ToAsyncEnumerable());
        return m.Object;
    }

    private static IDataExtractor<string> ThrowingExtractor(Exception ex)
    {
        async IAsyncEnumerable<string> Throw()
        {
            await Task.Yield();
            throw ex;
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
        var m = new Mock<IDataExtractor<string>>();
        m.Setup(x => x.ExtractAsync(It.IsAny<SyncContext>(), It.IsAny<SyncCheckpoint>(), It.IsAny<CancellationToken>()))
         .Returns(Throw());
        return m.Object;
    }

    private sealed class IdentityMapper : IRecordMapper<string, string>
    {
        public string Map(string external) => external;
    }

    private sealed class PredicateValidator : IRecordValidator<string>
    {
        private readonly Func<string, bool> _ok;
        private readonly string _error;
        public PredicateValidator(Func<string, bool> ok, string error) { _ok = ok; _error = error; }
        public bool IsValid(string record, out string? error)
        {
            if (_ok(record)) { error = null; return true; }
            error = _error;
            return false;
        }
    }

    private static SyncPipelineRequest<string, string> Request(
        SyncContext context,
        IDataExtractor<string> extractor,
        IRecordWriter<string> writer,
        int batchSize = 100,
        IRecordValidator<string>? validator = null,
        IRecordMapper<string, string>? mapper = null) => new()
    {
        Context = context,
        Extractor = extractor,
        Mapper = mapper ?? new IdentityMapper(),
        Writer = writer,
        Validator = validator,
        ExternalKeySelector = x => x,
        BatchSize = batchSize,
    };

    /// <summary>Writer that persists exactly <paramref name="returns"/> of the rows it receives.</summary>
    private static Mock<IRecordWriter<string>> Writer(Func<int, int> returns)
    {
        var m = new Mock<IRecordWriter<string>>();
        m.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((IReadOnlyList<string> b, CancellationToken _) => returns(b.Count));
        return m;
    }

    private static Mock<IRecordWriter<string>> FullWriter() => Writer(n => n);

    // ---------------- batch-size guard ----------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(SyncPipeline.MaxBatchSize + 1)]
    public async Task BatchSizeOutOfRange_Throws(int batchSize)
    {
        var (sut, _, _) = BuildSut();
        var req = Request(Context(), Extractor(Array.Empty<string>()), FullWriter().Object, batchSize);

        await sut.Invoking(s => s.RunAsync(req, CancellationToken.None))
                 .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BatchSizeAtMax_DoesNotThrowGuard()
    {
        var (sut, _, _) = BuildSut();
        var req = Request(Context(), Extractor(Array.Empty<string>()), FullWriter().Object, SyncPipeline.MaxBatchSize);

        var r = await sut.RunAsync(req, CancellationToken.None);
        r.Success.Should().BeTrue();
    }

    [Fact]
    public async Task NullRequest_Throws()
    {
        var (sut, _, _) = BuildSut();
        await sut.Invoking(s => s.RunAsync<string, string>(null!, CancellationToken.None))
                 .Should().ThrowAsync<ArgumentNullException>();
    }

    // ---------------- validation ----------------

    [Fact]
    public async Task Validator_DropsInvalid_CountsFailedAndProcessesRest()
    {
        var (sut, _, _) = BuildSut();
        var writer = FullWriter();
        var req = Request(Context(), Extractor(new[] { "good", "bad", "good2" }), writer.Object,
            validator: new PredicateValidator(r => r.StartsWith("good"), "rejected"));

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeTrue();
        r.RecordsProcessed.Should().Be(2);
        r.RecordsFailed.Should().Be(1);
        r.Warnings.Should().ContainSingle().Which.Should().Be("rejected");
    }

    [Fact]
    public async Task Validator_SameMessageTwice_RollsUpWithCount()
    {
        var (sut, _, _) = BuildSut();
        var req = Request(Context(), Extractor(new[] { "bad", "bad2", "ok" }), FullWriter().Object,
            validator: new PredicateValidator(r => r == "ok", "same-error"));

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.RecordsFailed.Should().Be(2);
        r.Warnings.Should().ContainSingle().Which.Should().Be("same-error (x2)");
    }

    [Fact]
    public async Task Validator_BlankErrorMessage_NotRecordedAsWarning()
    {
        var (sut, _, _) = BuildSut();
        var req = Request(Context(), Extractor(new[] { "bad", "ok" }), FullWriter().Object,
            validator: new PredicateValidator(r => r == "ok", "   "));

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.RecordsFailed.Should().Be(1);
        r.Warnings.Should().BeEmpty();
    }

    // ---------------- writer-skip accounting ----------------

    [Fact]
    public async Task WriterPartialSkip_WithProgress_StaysSuccessButReportsFailed()
    {
        var (sut, _, alert) = BuildSut();
        // 2 rows offered, writer persists 1 → 1 skipped, 1 processed.
        var req = Request(Context(), Extractor(new[] { "a", "b" }), Writer(_ => 1).Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeTrue();
        r.RecordsProcessed.Should().Be(1);
        r.RecordsFailed.Should().Be(1);
        // Writer-skipped > 0 fires a warning-severity alert even on a passing run.
        alert.Verify(a => a.PipelineFailureAsync(
            It.Is<SyncAlert>(x => x.Severity == "Warning"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriterZeroProgress_FailsRun()
    {
        var (sut, _, alert) = BuildSut();
        var req = Request(Context(), Extractor(new[] { "a", "b" }), Writer(_ => 0).Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("Zero-progress");
        r.RecordsFailed.Should().Be(2);
        // Zero rows persisted → Critical alert.
        alert.Verify(a => a.PipelineFailureAsync(
            It.Is<SyncAlert>(x => x.Severity == "Critical"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriterPartialSkip_StrictMode_FailsRun()
    {
        var (sut, _, _) = BuildSut(OptionsWith(p => p.FailRunOnAnyWriterSkip = true));
        var req = Request(Context(), Extractor(new[] { "a", "b" }), Writer(_ => 1).Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("Strict-visibility");
    }

    // ---------------- idempotency hard cap ----------------

    [Fact]
    public async Task IdempotencyHardCap_Reached_FailsRun()
    {
        var (sut, _, _) = BuildSut(OptionsWith(p => p.MaxIdempotencyKeysPerRun = 1));
        var req = Request(Context(), Extractor(new[] { "k1", "k2" }), FullWriter().Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("hard cap");
    }

    [Fact]
    public async Task IdempotencyHardCap_Zero_Disabled_Succeeds()
    {
        var (sut, _, _) = BuildSut(OptionsWith(p => p.MaxIdempotencyKeysPerRun = 0));
        var req = Request(Context(), Extractor(new[] { "k1", "k2" }), FullWriter().Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);
        r.Success.Should().BeTrue();
        r.RecordsProcessed.Should().Be(2);
    }

    // ---------------- replay detection ----------------

    [Fact]
    public async Task ReplayDetected_OnRetryAttempt_LogsReplay()
    {
        var (sut, logger, _) = BuildSut();
        var ctx = Context(attempt: 2);
        var req = Request(ctx, Extractor(new[] { "a" }), FullWriter().Object);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeTrue();
        logger.Verify(l => l.LogInformation(ctx.CorrelationId,
            It.Is<string>(s => s.Contains("replay detected")), It.IsAny<object?[]>()), Times.Once);
    }

    // ---------------- per-batch writer retry ----------------

    [Fact]
    public async Task WriterRetry_TransientFailureThenSuccess_Recovers()
    {
        var (sut, _, _) = BuildSut(OptionsWith(p =>
        {
            p.PerBatchWriterRetryAttempts = 2;
            p.PerBatchWriterRetryBackoff = TimeSpan.Zero;
        }));
        var writer = new Mock<IRecordWriter<string>>();
        writer.SetupSequence(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("transient"))
              .ReturnsAsync(1);
        var req = Request(Context(), Extractor(new[] { "a" }), writer.Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeTrue();
        r.RecordsProcessed.Should().Be(1);
        writer.Verify(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WriterRetry_BudgetExhausted_FailsRun()
    {
        var (sut, _, _) = BuildSut(OptionsWith(p =>
        {
            p.PerBatchWriterRetryAttempts = 1;
            p.PerBatchWriterRetryBackoff = TimeSpan.Zero;
        }));
        var writer = new Mock<IRecordWriter<string>>();
        writer.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("permanent"));
        var req = Request(Context(), Extractor(new[] { "a" }), writer.Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("permanent");
        // 1 initial + 1 retry = 2 attempts before propagating.
        writer.Verify(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ---------------- exception arms ----------------

    [Fact]
    public async Task ExtractorThrows_ReturnsFailedAndSendsAlert()
    {
        var (sut, _, alert) = BuildSut();
        var req = Request(Context(), ThrowingExtractor(new InvalidOperationException("boom")), FullWriter().Object);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Be("boom");
        alert.Verify(a => a.PipelineFailureAsync(It.IsAny<SyncAlert>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlertHookThrows_IsSwallowed_StillReturnsFailed()
    {
        var (sut, _, alert) = BuildSut();
        alert.Setup(a => a.PipelineFailureAsync(It.IsAny<SyncAlert>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new Exception("alert sink down"));
        var req = Request(Context(), ThrowingExtractor(new InvalidOperationException("boom")), FullWriter().Object);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task NoAlertHookConfigured_ExtractorThrows_StillReturnsFailed()
    {
        var (sut, _, _) = BuildSut(withAlert: false);
        var req = Request(Context(), ThrowingExtractor(new InvalidOperationException("boom")), FullWriter().Object);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public async Task GenuineCancellation_Rethrows()
    {
        var (sut, _, _) = BuildSut();
        using var cts = new CancellationTokenSource();
        var writer = new Mock<IRecordWriter<string>>();
        writer.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
              .Returns(async (IReadOnlyList<string> _, CancellationToken __) =>
              {
                  cts.Cancel();
                  cts.Token.ThrowIfCancellationRequested();
                  return 0;
              });
        var req = Request(Context(), Extractor(new[] { "a" }), writer.Object, batchSize: 10);

        await sut.Invoking(s => s.RunAsync(req, cts.Token))
                 .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SpuriousOCE_WithoutCancellation_ReturnsFailed()
    {
        var (sut, _, _) = BuildSut();
        // Writer throws OCE but no token is signaled → spurious → Failed, not rethrow.
        var writer = new Mock<IRecordWriter<string>>();
        writer.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new OperationCanceledException());
        var req = Request(Context(), Extractor(new[] { "a" }), writer.Object, batchSize: 10);

        var r = await sut.RunAsync(req, CancellationToken.None);

        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("Spurious");
    }

    // ---------------- mapper ----------------

    private sealed class UpperMapper : IRecordMapper<string, string>
    {
        public string Map(string external) => external.ToUpperInvariant();
    }

    [Fact]
    public async Task Mapper_TransformsBeforeWriter()
    {
        var (sut, _, _) = BuildSut();
        IReadOnlyList<string>? written = null;
        var writer = new Mock<IRecordWriter<string>>();
        writer.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((IReadOnlyList<string> b, CancellationToken _) => { written = b; return b.Count; });
        var req = Request(Context(), Extractor(new[] { "ab" }), writer.Object, mapper: new UpperMapper());

        await sut.RunAsync(req, CancellationToken.None);

        written.Should().ContainSingle().Which.Should().Be("AB");
    }
}
