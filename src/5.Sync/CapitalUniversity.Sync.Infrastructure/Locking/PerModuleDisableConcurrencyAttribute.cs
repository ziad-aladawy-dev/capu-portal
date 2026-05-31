using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;

namespace CapitalUniversity.Sync.Infrastructure.Locking;

/// <summary>
/// Prevents two Hangfire executions of the same (module, direction) pair from running
/// concurrently. Two DIFFERENT modules (or the two directions of the same module) may
/// still execute in parallel.
///
/// Mechanism: acquires a Hangfire <c>IStorageConnection.AcquireDistributedLock</c>
/// keyed by <c>sync-module:{moduleName}:{direction}</c>. The lock is implemented by
/// the configured Hangfire storage (SQL Server in our case) and is released on the
/// worker that owns it.
///
/// Behavior on conflict: the second worker BLOCKS waiting for the lock up to
/// <see cref="TimeoutSeconds"/>. If the timeout elapses, Hangfire throws
/// <c>DistributedLockTimeoutException</c>, the executor's retry policy engages,
/// and the job is rescheduled.
/// </summary>
public sealed class PerModuleDisableConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private const string LockKey = "Sync:PerModuleConcurrencyLock";

    public int TimeoutSeconds { get; }

    public PerModuleDisableConcurrencyAttribute(int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "Must be > 0.");
        }
        TimeoutSeconds = timeoutSeconds;
    }

    public void OnPerforming(PerformingContext filterContext)
    {
        ArgumentNullException.ThrowIfNull(filterContext);

        var args = filterContext.BackgroundJob.Job.Args;
        if (args is null || args.Count < 2)
        {
            return;
        }

        var moduleName = args[0] as string ?? "<unknown-module>";
        var directionStr = args[1]?.ToString() ?? "<unknown-direction>";
        var resource = $"sync-module:{moduleName}:{directionStr}";

        var distributedLock = filterContext.Connection
            .AcquireDistributedLock(resource, TimeSpan.FromSeconds(TimeoutSeconds));

        filterContext.Items[LockKey] = distributedLock;
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        ArgumentNullException.ThrowIfNull(filterContext);

        if (filterContext.Items.TryGetValue(LockKey, out var lockObj) && lockObj is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}