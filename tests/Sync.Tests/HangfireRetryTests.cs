using System.Reflection;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Infrastructure.Locking;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Entities;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Verifies the Hangfire-retry contract: the executor is decorated with the
/// configured <see cref="AutomaticRetryAttribute"/> + <see cref="PerModuleDisableConcurrencyAttribute"/>
/// budget, and once retries are exhausted (Hangfire transitions the job to
/// <see cref="FailedState"/>) the <see cref="SyncDeadLetterFilter"/> writes the
/// dead-letter audit row and flips the corresponding sync_runs row to
/// <see cref="SyncRunStatus.DeadLettered"/>.
/// </summary>
public class HangfireRetryTests
{
    [Fact]
    public void ExecuteAsync_IsDecoratedWithExpectedRetryPolicy()
    {
        // The retry policy is the contract Hangfire reads off the method. If
        // someone silently tweaks Attempts / DelaysInSeconds / OnAttemptsExceeded
        // the production retry behaviour changes — this guard locks the numbers.
        var method = typeof(SyncModuleExecutor)
            .GetMethod(nameof(SyncModuleExecutor.ExecuteAsync), BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();

        var retry = method!.GetCustomAttribute<AutomaticRetryAttribute>();
        retry.Should().NotBeNull("ExecuteAsync must carry [AutomaticRetry] so transient failures are re-tried before dead-lettering");
        retry!.Attempts.Should().Be(4, "production retry budget is 4 — changing this changes the dead-letter SLO");
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail,
            "must end in FailedState so SyncDeadLetterFilter can record the terminal transition");
        retry.DelaysInSeconds.Should().Equal(60, 300, 900, 3600);

        var concurrency = method!.GetCustomAttribute<PerModuleDisableConcurrencyAttribute>();
        concurrency.Should().NotBeNull("ExecuteAsync must carry [PerModuleDisableConcurrency] so duplicate workers don't double-run a module");
        concurrency!.TimeoutSeconds.Should().Be(1,
            "Phase X stability fix: the filter now SKIPS (no-throw) on lock conflict, so a tiny timeout is enough; " +
            "a larger value would re-introduce worker-thread starvation under the cron-storm load scenario");
    }

    [Fact]
    public async Task DeadLetterFilter_OnTerminalFailure_WritesAuditAndFlipsRun()
    {
        // Arrange — a Running sync_runs row exists for the correlation we're about
        // to terminally fail.
        await using var harness = await DeadLetterHarness.CreateAsync(seedRun: true);
        var filter = new SyncDeadLetterFilter(harness.ScopeFactory, harness.Logger);

        var exception = new InvalidOperationException("upstream sink is down");
        var context = harness.BuildApplyStateContext(new FailedState(exception), retryCountParam: "3");

        // Act
        filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert — dead-letter row was written with the right metadata.
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        var dl = await db.DeadLetters.AsNoTracking()
            .SingleAsync(d => d.HangfireJobId == harness.JobId);
        dl.CorrelationId.Should().Be(harness.CorrelationId);
        dl.ModuleName.Should().Be("students");
        dl.Direction.Should().Be(SyncDirection.Pull);
        dl.AttemptedCount.Should().Be(4, "RetryCount=3 means this was the 4th attempt");
        dl.LastError.Should().Contain("upstream sink is down");

        // sync_runs row flipped Running → DeadLettered.
        var run = await db.Runs.AsNoTracking()
            .SingleAsync(r => r.CorrelationId == harness.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.DeadLettered);
        run.CompletedAt.Should().NotBeNull();
        run.LastError.Should().Contain("upstream sink is down");
    }

    [Fact]
    public async Task DeadLetterFilter_DuplicateInvocation_IsIdempotent()
    {
        // Hangfire occasionally re-applies FailedState (e.g. the documented
        // double-FailedState artifact). The unique index on HangfireJobId is the
        // race-stopper: the second INSERT raises a constraint violation that the
        // filter recognises as the idempotency signal. One job → one dead-letter
        // row, one terminal transition.
        await using var harness = await DeadLetterHarness.CreateAsync(seedRun: true);
        var filter = new SyncDeadLetterFilter(harness.ScopeFactory, harness.Logger);

        var context = harness.BuildApplyStateContext(
            new FailedState(new InvalidOperationException("boom")),
            retryCountParam: "3");

        // Act — fire twice as Hangfire would in the double-apply case.
        filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());
        filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert — still exactly one dead-letter row; still exactly one run row.
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        (await db.DeadLetters.CountAsync(d => d.HangfireJobId == harness.JobId))
            .Should().Be(1);

        var run = await db.Runs.AsNoTracking()
            .SingleAsync(r => r.CorrelationId == harness.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.DeadLettered, "second OnStateApplied must not overwrite the terminal row");
    }

    [Fact]
    public async Task DeadLetterFilter_OperationCanceled_DoesNotWriteDeadLetter()
    {
        // Cancellation is not a failure — host shutdown / cooperative cancel should
        // never poison the run with a dead-letter row.
        await using var harness = await DeadLetterHarness.CreateAsync(seedRun: true);
        var filter = new SyncDeadLetterFilter(harness.ScopeFactory, harness.Logger);

        var context = harness.BuildApplyStateContext(
            new FailedState(new OperationCanceledException("worker stopping")),
            retryCountParam: "0");

        // Act
        filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());

        // Assert
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        (await db.DeadLetters.AnyAsync()).Should().BeFalse();
        var run = await db.Runs.AsNoTracking()
            .SingleAsync(r => r.CorrelationId == harness.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.Running, "cancellation must not mutate the run; the executor's MarkCancelled owns that transition");
    }

    [Fact]
    public async Task DeadLetterFilter_ConcurrentFailuresForSameJob_ProduceExactlyOneRow()
    {
        // Hardening guarantee for the dead-letter table integrity task: even
        // when multiple workers race to apply the terminal FailedState for the
        // SAME Hangfire job id (the classic Hangfire double-FailedState plus
        // the "two workers picked up the same job after a fail-over" pattern),
        // only one sync_dead_letters row may exist. The unique index
        // IX_dead_letters_HangfireJobId enforces this at the database; the
        // filter relies on that constraint, not on any in-process pre-check.
        await using var harness = await DeadLetterHarness.CreateAsync(seedRun: true);
        var filter = new SyncDeadLetterFilter(harness.ScopeFactory, harness.Logger);

        var context = harness.BuildApplyStateContext(
            new FailedState(new InvalidOperationException("upstream went away")),
            retryCountParam: "3");

        // Act — fire 8 parallel terminal transitions for the same job id. The
        // shared SQLite connection serialises writes internally; each thread
        // still races for the constraint check, so exactly one INSERT survives.
        const int concurrency = 8;
        using var gate = new ManualResetEventSlim(initialState: false);
        var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(() =>
        {
            gate.Wait();
            filter.OnStateApplied(context, Mock.Of<IWriteOnlyTransaction>());
        })).ToArray();

        gate.Set();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert — exactly one dead-letter row for this job id.
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        (await db.DeadLetters.CountAsync(d => d.HangfireJobId == harness.JobId))
            .Should().Be(1, "the unique index must collapse concurrent inserts to a single audit row");

        // The run row was flipped exactly once; subsequent racers saw status
        // != Running and skipped the mutation (see filter step 2 guard).
        var run = await db.Runs.AsNoTracking()
            .SingleAsync(r => r.CorrelationId == harness.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.DeadLettered);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeadLetterTable_DeclaresUniqueIndexOnHangfireJobId()
    {
        // Model-level contract test. The filter's idempotency hinges on a unique
        // constraint at the database; this test pins the configuration so a
        // future refactor cannot silently drop the index without failing CI.
        await using var harness = await DeadLetterHarness.CreateAsync(seedRun: false);
        using var scope = harness.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        var entity = db.Model.FindEntityType(typeof(SyncDeadLetterEntity))!;
        var hangfireIndex = entity.GetIndexes()
            .Single(ix => ix.Properties.Count == 1 &&
                          ix.Properties[0].Name == nameof(SyncDeadLetterEntity.HangfireJobId));

        hangfireIndex.IsUnique.Should().BeTrue(
            "the dead-letter race-stopper is the unique index on HangfireJobId — removing IsUnique reintroduces the duplicate-row race");
    }

    // ── Test harness ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQLite-backed test harness. SQLite (unlike EF's InMemory provider)
    /// honors unique-index constraints, so the dead-letter idempotency
    /// contract is exercised end-to-end exactly as it runs in production
    /// SQL Server — a duplicate INSERT raises a constraint violation that
    /// surfaces as <see cref="DbUpdateException"/> with a SQLite-flavoured
    /// inner exception. The filter's <c>IsUniqueConstraintViolation</c>
    /// reflection check recognises both shapes (SQL Server 2601/2627 and
    /// the SQLite "UNIQUE constraint failed" string).
    /// </summary>
    private sealed class DeadLetterHarness : IAsyncDisposable
    {
        public IServiceProvider Provider { get; }
        public IServiceScopeFactory ScopeFactory { get; }
        public ISyncLogger Logger { get; }
        public Guid CorrelationId { get; }
        public string JobId { get; }
        public SyncRunMetadata Metadata { get; }
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        private DeadLetterHarness(
            ServiceProvider provider,
            IServiceScopeFactory scopeFactory,
            ISyncLogger logger,
            Guid correlationId,
            string jobId,
            SyncRunMetadata metadata,
            SqliteConnection connection)
        {
            Provider = provider;
            _serviceProvider = provider;
            ScopeFactory = scopeFactory;
            Logger = logger;
            CorrelationId = correlationId;
            JobId = jobId;
            Metadata = metadata;
            _connection = connection;
        }

        public static async Task<DeadLetterHarness> CreateAsync(bool seedRun)
        {
            // A single shared in-memory SQLite connection — opened once, reused
            // across DbContext scopes — gives us a real relational database that
            // honors the unique index without spinning a SQL Server container.
            // The connection's lifetime owns the DB; closing it discards the
            // schema. The harness implements IAsyncDisposable to guarantee that.
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddDbContext<SyncDbContext>(o => o.UseSqlite(connection),
                contextLifetime: ServiceLifetime.Scoped);
            services.AddLogging();
            var loggerMock = new Mock<ISyncLogger>();
            services.AddSingleton(loggerMock.Object);

            var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

            // EnsureCreated reads the runtime model (including the unique index
            // on HangfireJobId added by SyncDeadLetterConfiguration) and emits
            // the SQLite-flavoured CREATE TABLE / CREATE UNIQUE INDEX. The
            // "sync.dead_letters" schema-qualified name becomes a SQLite table
            // literally named "sync.dead_letters" — EF resolves it correctly
            // because the model carries the same fully-qualified identifier.
            using (var initScope = provider.CreateScope())
            {
                var db = initScope.ServiceProvider.GetRequiredService<SyncDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var correlationId = Guid.NewGuid();
            var jobId = "hangfire-job-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var metadata = new SyncRunMetadata
            {
                CorrelationId = correlationId,
                TriggeredBy = "retry-test"
            };

            if (seedRun)
            {
                using var seedScope = provider.CreateScope();
                var db = seedScope.ServiceProvider.GetRequiredService<SyncDbContext>();
                db.Runs.Add(new SyncRunEntity
                {
                    CorrelationId = correlationId,
                    ModuleName = "students",
                    Direction = SyncDirection.Pull,
                    TriggeredBy = "retry-test",
                    Queue = "default",
                    Status = SyncRunStatus.Running,
                    AttemptCount = 4,
                    HangfireJobId = jobId,
                    EnqueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                });
                await db.SaveChangesAsync();
            }

            return new DeadLetterHarness(provider, scopeFactory, loggerMock.Object, correlationId, jobId, metadata, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        public ApplyStateContext BuildApplyStateContext(IState newState, string retryCountParam)
        {
            var connectionMock = new Mock<IStorageConnection>(MockBehavior.Loose);
            connectionMock
                .Setup(c => c.GetJobParameter(JobId, "RetryCount"))
                .Returns(retryCountParam);

            var method = typeof(SyncModuleExecutor).GetMethod(nameof(SyncModuleExecutor.ExecuteAsync))!;
            var cancellationToken = new JobCancellationToken(canceled: false);
            var job = new Job(
                typeof(SyncModuleExecutor),
                method,
                new object?[] { "students", SyncDirection.Pull, Metadata, null, cancellationToken }!);

            var bgJob = new BackgroundJob(JobId, job, DateTime.UtcNow);
            var storageMock = new Mock<JobStorage>(MockBehavior.Loose);
            var tx = new Mock<IWriteOnlyTransaction>().Object;

            return new ApplyStateContext(
                storageMock.Object,
                connectionMock.Object,
                tx,
                bgJob,
                newState,
                oldStateName: ProcessingState.StateName);
        }
    }
}
