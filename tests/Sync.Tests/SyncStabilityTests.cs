using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Courses;
using CapitalUniversity.Sync.Finance;
using CapitalUniversity.Sync.Host.Scheduling;
using CapitalUniversity.Sync.Infrastructure.Locking;
using CapitalUniversity.Sync.Schedules;
using CapitalUniversity.Sync.Staff;
using CapitalUniversity.Sync.Student;
using FluentAssertions;
using Hangfire.Storage;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Stability + load-simulation tests for the sync engine.
///
/// <list type="bullet">
///   <item><see cref="StaggerDistributesAllTenModuleTriggersAcrossTheMinute"/>
///         pins the cron-storm mitigation — every (module, direction) pair
///         lands on a distinct deterministic offset within the firing minute.</item>
///   <item><see cref="StaggerIsDeterministicAcrossInvocations"/> pins the
///         stability guarantee — the same input always produces the same
///         offset across process restarts.</item>
///   <item><see cref="ConcurrentLockAcquire_NineOfTenSkipQuietly"/> pins the
///         retry-amplification mitigation — when the per-(module, direction)
///         lock is held, additional workers SKIP instead of throwing and
///         engaging Hangfire's retry backoff.</item>
///   <item><see cref="LockReleaseAllowsSubsequentAcquireToSucceed"/> proves
///         the skip-on-conflict path doesn't leak — once the holder releases,
///         the next worker takes the lock cleanly.</item>
/// </list>
/// </summary>
public class SyncStabilityTests
{
    /// <summary>
    /// All 10 cron-driven (module, direction) pairs must hash to a unique
    /// sub-minute offset in [0, StaggerSeconds). Proves that a minute's
    /// worth of cron firings spreads dispatch work across the minute
    /// instead of stacking at :00.
    /// </summary>
    [Fact]
    public void StaggerDistributesAllTenModuleTriggersAcrossTheMinute()
    {
        var pairs = AllModuleDirectionPairs();

        var offsets = pairs
            .Select(p => SyncRecurringTrigger.ComputeStaggerSeconds(p.module, p.direction))
            .ToList();

        offsets.Should().AllSatisfy(o =>
        {
            o.Should().BeGreaterThanOrEqualTo(0);
            o.Should().BeLessThan(SyncRecurringTrigger.StaggerSeconds,
                "the offset must never bleed into the next minute's cron tick");
        });

        offsets.Distinct().Should().HaveCount(pairs.Length,
            $"all {pairs.Length} module-direction pairs must land on distinct seconds — collisions defeat the staggering");
    }

    /// <summary>
    /// The hash function MUST be stable so operators see a predictable
    /// per-job dispatch second in audit logs across process restarts.
    /// </summary>
    [Fact]
    public void StaggerIsDeterministicAcrossInvocations()
    {
        foreach (var p in AllModuleDirectionPairs())
        {
            var first  = SyncRecurringTrigger.ComputeStaggerSeconds(p.module, p.direction);
            var second = SyncRecurringTrigger.ComputeStaggerSeconds(p.module, p.direction);
            var third  = SyncRecurringTrigger.ComputeStaggerSeconds(p.module, p.direction);
            (second, third).Should().Be((first, first),
                $"jitter for ({p.module},{p.direction}) must be stable across calls");
        }
    }

    /// <summary>
    /// Simulates 10 cron-fired workers all trying to execute the same
    /// (module, direction) pair in the same tick. With the held lock, nine
    /// of them must see the lock conflict and SKIP (no exception, no retry
    /// burst); one must actually take the lock.
    /// <para>
    /// This is the exact scenario that produces a "doomed retries" storm
    /// without skip-on-conflict: each blocked worker would have thrown
    /// <see cref="DistributedLockTimeoutException"/>, kicked Hangfire's
    /// 60/300/900/3600-second retry backoff, and piled doomed jobs into the
    /// queue at every cron tick.
    /// </para>
    /// </summary>
    [Fact]
    public void ConcurrentLockAcquire_NineOfTenSkipQuietly()
    {
        const int workers = 10;
        var connection = new SingleSlotConnection();
        var resource = "sync-module:students:Pull";

        var acquired = 0;
        var skipped = new List<string?>();

        for (var i = 0; i < workers; i++)
        {
            var ok = PerModuleDisableConcurrencyAttribute.TryAcquireOrSkip(
                connection.Connection,
                resource,
                TimeSpan.FromSeconds(1),
                out _,
                out var reason);

            if (ok) acquired++;
            else skipped.Add(reason);
        }

        acquired.Should().Be(1,
            "exactly one worker takes the lock; the others must short-circuit");
        skipped.Should().HaveCount(workers - 1);
        skipped.Should().AllSatisfy(r =>
            r.Should().Contain("Duplicate cron-fired execution skipped"),
            "every skipped worker must record the reason so operators can see it in the Hangfire dashboard");
    }

    /// <summary>
    /// Once the lock holder releases, the next worker must be able to
    /// acquire cleanly — proves the storm-mitigation isn't a one-way
    /// permanent lockout.
    /// </summary>
    [Fact]
    public void LockReleaseAllowsSubsequentAcquireToSucceed()
    {
        var connection = new SingleSlotConnection();
        var resource = "sync-module:students:Pull";

        // First worker acquires.
        PerModuleDisableConcurrencyAttribute.TryAcquireOrSkip(
            connection.Connection, resource, TimeSpan.FromSeconds(1),
            out var held, out _).Should().BeTrue();

        // Second worker skips because the lock is held.
        PerModuleDisableConcurrencyAttribute.TryAcquireOrSkip(
            connection.Connection, resource, TimeSpan.FromSeconds(1),
            out _, out _).Should().BeFalse();

        // First worker disposes the lock (Hangfire does this in OnPerformed).
        held!.Dispose();

        // Third worker now acquires cleanly.
        PerModuleDisableConcurrencyAttribute.TryAcquireOrSkip(
            connection.Connection, resource, TimeSpan.FromSeconds(1),
            out var newHolder, out _).Should().BeTrue();
        newHolder.Should().NotBeNull();
    }

    private static (string module, SyncDirection direction)[] AllModuleDirectionPairs() => new[]
    {
        (StudentSyncModule.Name,   SyncDirection.Pull),
        (StudentSyncModule.Name,   SyncDirection.Push),
        (StaffSyncModule.Name,     SyncDirection.Pull),
        (StaffSyncModule.Name,     SyncDirection.Push),
        (CoursesSyncModule.Name,   SyncDirection.Pull),
        (CoursesSyncModule.Name,   SyncDirection.Push),
        (FinanceSyncModule.Name,   SyncDirection.Pull),
        (FinanceSyncModule.Name,   SyncDirection.Push),
        (SchedulesSyncModule.Name, SyncDirection.Pull),
        (SchedulesSyncModule.Name, SyncDirection.Push),
    };

    /// <summary>
    /// Mocked <see cref="IStorageConnection"/> that mimics a SQL distributed
    /// lock: the first <c>AcquireDistributedLock</c> call returns a disposable
    /// handle; subsequent calls throw <see cref="DistributedLockTimeoutException"/>
    /// until that handle is disposed. Strict mock — any other call fails the
    /// test, so the filter MUST NOT touch any other storage API.
    /// </summary>
    private sealed class SingleSlotConnection
    {
        private int _held;
        private readonly Mock<IStorageConnection> _mock = new(MockBehavior.Strict);

        public SingleSlotConnection()
        {
            _mock
                .Setup(c => c.AcquireDistributedLock(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns<string, TimeSpan>((resource, _) =>
                {
                    if (Interlocked.CompareExchange(ref _held, 1, 0) != 0)
                    {
                        throw new DistributedLockTimeoutException(resource);
                    }
                    return new Release(this);
                });
        }

        public IStorageConnection Connection => _mock.Object;

        private sealed class Release : IDisposable
        {
            private readonly SingleSlotConnection _owner;
            public Release(SingleSlotConnection owner) => _owner = owner;
            public void Dispose() => Interlocked.Exchange(ref _owner._held, 0);
        }
    }
}
