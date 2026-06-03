using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Infrastructure.Locking;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Exercises the SQL-backed distributed-lock contract that
/// <see cref="PerModuleDisableConcurrencyAttribute"/> relies on.
///
/// The Hangfire SqlServer storage implements <c>IStorageConnection.AcquireDistributedLock</c>
/// as a row-level lock keyed by the resource string; a second worker hitting the same
/// resource blocks until either the lock is released or the timeout elapses, at which
/// point Hangfire throws <see cref="DistributedLockTimeoutException"/>. These tests use a
/// shared in-memory lock-table to simulate that contract faithfully without needing a
/// real SQL Server container — same race semantics, hermetic + fast.
/// </summary>
public class SqlBackedRaceTests
{
    [Fact]
    public async Task TwoConcurrentExecutions_SameModuleAndDirection_OnlyOneAcquires()
    {
        // Arrange — shared "SQL" lock table; one resource per (module, direction).
        var lockTable = new FakeSqlLockTable();
        var filter = new PerModuleDisableConcurrencyAttribute(timeoutSeconds: 1);

        // Both contexts target the SAME (students, Pull) resource. Phase X
        // stability update: the filter now SKIPS (Canceled=true, no throw)
        // when the lock is held — see PerModuleDisableConcurrencyAttribute's
        // class doc for the retry-amplification rationale.
        var ctxA = BuildPerformingContext(lockTable, "students", SyncDirection.Pull, jobId: "job-A");
        var ctxB = BuildPerformingContext(lockTable, "students", SyncDirection.Pull, jobId: "job-B");

        // Act — fire both filters concurrently. Use a gate so they start at the
        // same instant; one acquires the lock, the other sees the conflict and
        // skips silently.
        using var gate = new ManualResetEventSlim(initialState: false);
        Exception? exA = null;
        Exception? exB = null;

        var taskA = Task.Run(() =>
        {
            gate.Wait();
            try { filter.OnPerforming(ctxA); }
            catch (Exception ex) { exA = ex; }
        });
        var taskB = Task.Run(() =>
        {
            gate.Wait();
            try { filter.OnPerforming(ctxB); }
            catch (Exception ex) { exB = ex; }
        });

        gate.Set();
        await Task.WhenAll(taskA, taskB).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — neither call should THROW (skip-on-conflict means no exception
        // path engages Hangfire's retry backoff). Exactly one filter must have
        // marked its context as Canceled = true — the SQL-lock holder proceeds,
        // the loser exits cleanly. Symmetry is non-deterministic (whichever
        // scheduler-served task gets there first wins).
        exA.Should().BeNull("skip-on-conflict path must not throw");
        exB.Should().BeNull("skip-on-conflict path must not throw");

        var cancelledContexts = new[] { ctxA, ctxB }.Count(c => c.Canceled);
        cancelledContexts.Should().Be(1, "exactly one worker should have skipped via Canceled = true");
        lockTable.HeldResources.Should().ContainSingle().Which.Should().Be("sync-module:students:Pull");
    }

    [Fact]
    public void TwoConcurrentExecutions_DifferentDirection_BothAcquire()
    {
        // Arrange — same module, different directions. Per the lock's resource
        // naming the two resources don't collide, so the SQL-backed lock allows
        // both workers to proceed in parallel.
        var lockTable = new FakeSqlLockTable();
        var filter = new PerModuleDisableConcurrencyAttribute(timeoutSeconds: 1);

        var ctxPull = BuildPerformingContext(lockTable, "students", SyncDirection.Pull, jobId: "pull-1");
        var ctxPush = BuildPerformingContext(lockTable, "students", SyncDirection.Push, jobId: "push-1");

        // Act
        filter.OnPerforming(ctxPull);
        filter.OnPerforming(ctxPush);

        // Assert — both resources are held; no exception was raised.
        lockTable.HeldResources.Should().BeEquivalentTo(new[]
        {
            "sync-module:students:Pull",
            "sync-module:students:Push"
        });
    }

    [Fact]
    public void OnPerformed_ReleasesLock_SoSubsequentRunReacquires()
    {
        // Arrange
        var lockTable = new FakeSqlLockTable();
        var filter = new PerModuleDisableConcurrencyAttribute(timeoutSeconds: 1);

        var ctxFirst = BuildPerformingContext(lockTable, "students", SyncDirection.Pull, jobId: "first");

        // Act 1 — first worker acquires.
        filter.OnPerforming(ctxFirst);
        lockTable.HeldResources.Should().Contain("sync-module:students:Pull");

        // Act 2 — first worker completes; lock must be released so the next
        // scheduled run can pick the same module up again.
        var performedCtx = new PerformedContext(ctxFirst, result: null, canceled: false, exception: null);
        filter.OnPerformed(performedCtx);

        lockTable.HeldResources.Should().BeEmpty("OnPerformed must dispose the held distributed lock");

        // Act 3 — a new run for the same resource succeeds; no carryover state.
        var ctxSecond = BuildPerformingContext(lockTable, "students", SyncDirection.Pull, jobId: "second");
        filter.OnPerforming(ctxSecond);

        lockTable.HeldResources.Should().ContainSingle().Which.Should().Be("sync-module:students:Pull");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a real Hangfire <see cref="PerformingContext"/> backed by a mocked
    /// <see cref="IStorageConnection"/> whose <c>AcquireDistributedLock</c> delegates
    /// into the shared <see cref="FakeSqlLockTable"/>. The job is constructed with
    /// the same shape <see cref="SyncModuleExecutor.ExecuteAsync"/> uses, so the
    /// filter resolves <c>Job.Args[0..1]</c> exactly as it would in production.
    /// </summary>
    private static PerformingContext BuildPerformingContext(
        FakeSqlLockTable lockTable,
        string moduleName,
        SyncDirection direction,
        string jobId)
    {
        var connectionMock = new Mock<IStorageConnection>(MockBehavior.Loose);
        connectionMock
            .Setup(c => c.AcquireDistributedLock(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns((string resource, TimeSpan timeout) => lockTable.Acquire(resource, timeout));

        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "race-test"
        };

        // JobCancellationToken.Null can be unreliable across Hangfire minor versions
        // (some 1.8.x bits null-init the field). Constructing the token explicitly
        // sidesteps that and matches what the production Hangfire pipeline does.
        var cancellationToken = new JobCancellationToken(canceled: false);

        var method = typeof(SyncModuleExecutor).GetMethod(nameof(SyncModuleExecutor.ExecuteAsync))!;
        var job = new Job(
            typeof(SyncModuleExecutor),
            method,
            new object?[] { moduleName, direction, metadata, null, cancellationToken }!);

        var bgJob = new BackgroundJob(jobId, job, DateTime.UtcNow);
        var storageMock = new Mock<JobStorage>(MockBehavior.Loose);
        var perform = new PerformContext(
            storageMock.Object,
            connectionMock.Object,
            bgJob,
            cancellationToken);
        return new PerformingContext(perform);
    }

    /// <summary>
    /// In-memory stand-in for the Hangfire SqlServer distributed-lock table.
    /// Holds a hash-set of acquired resources; a second <see cref="Acquire"/> for
    /// an already-held resource throws <see cref="DistributedLockTimeoutException"/>
    /// after the timeout elapses — exactly matching the SQL backend's contract.
    /// </summary>
    private sealed class FakeSqlLockTable
    {
        private readonly object _gate = new();
        private readonly HashSet<string> _held = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> HeldResources
        {
            get { lock (_gate) { return _held.ToArray(); } }
        }

        public IDisposable Acquire(string resource, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                lock (_gate)
                {
                    if (_held.Add(resource))
                    {
                        return new Releaser(this, resource);
                    }
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new DistributedLockTimeoutException(resource);
                }

                Thread.Sleep(10);
            }
        }

        private void Release(string resource)
        {
            lock (_gate) { _held.Remove(resource); }
        }

        private sealed class Releaser : IDisposable
        {
            private readonly FakeSqlLockTable _table;
            private readonly string _resource;
            private int _disposed;

            public Releaser(FakeSqlLockTable table, string resource)
            {
                _table = table;
                _resource = resource;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _table.Release(_resource);
                }
            }
        }
    }
}
