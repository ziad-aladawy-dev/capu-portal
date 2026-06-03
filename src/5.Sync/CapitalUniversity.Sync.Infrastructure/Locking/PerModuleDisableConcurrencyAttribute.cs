using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;

namespace CapitalUniversity.Sync.Infrastructure.Locking;

/// <summary>
/// Prevents two Hangfire executions of the same (module, direction) pair from running
/// concurrently. Two DIFFERENT modules (or the two directions of the same module) may
/// still execute in parallel.
///
/// <para>
/// Mechanism: acquires a Hangfire <c>IStorageConnection.AcquireDistributedLock</c>
/// keyed by <c>sync-module:{moduleName}:{direction}</c>. The lock is implemented by
/// the configured Hangfire storage (SQL Server in our case) and is released on the
/// worker that owns it.
/// </para>
///
/// <para>
/// <b>Behavior on conflict (skip-on-conflict).</b> The second worker tries to
/// acquire the lock with a short bounded wait (<see cref="TimeoutSeconds"/>);
/// if the lock is still held, the worker SKIPS the execution by setting
/// <c>PerformingContext.Canceled = true</c>. Hangfire treats a cancelled
/// execution as succeeded (no retry, no exception), so the
/// <c>[AutomaticRetry(Attempts=4)]</c> backoff never engages on a duplicate.
/// </para>
///
/// <para>
/// <b>Why this matters.</b> A 1-minute cron firing a slow sync job creates
/// the classic "doomed retries" storm: every cron tick enqueues a fresh job
/// that waits-and-throws on the lock; each thrown job kicks Hangfire's
/// 60/300/900/3600-second retry ladder; within 5 minutes the queue is
/// saturated with 5+ identical jobs each carrying their own retry budget.
/// Silent skip-on-conflict turns those duplicates into clean no-ops at the
/// queue level — the in-flight original keeps running, the duplicates exit,
/// no retry burst.
/// </para>
///
/// <para>
/// <b>What "succeeded" looks like in audit.</b> When the worker skips here,
/// the executor's <c>ExecuteAsync</c> body never runs — so neither
/// <c>OpenRunAsync</c> (self-heal) nor <c>MarkStarted</c> is called. The
/// <c>sync.runs</c> row written earlier by the dispatcher stays in
/// <c>Enqueued</c>; the orphan reaper closes the audit window after its
/// grace period elapses. The Hangfire job's state moves to <c>Succeeded</c>
/// with a <c>SkippedReason</c> parameter explaining why.
/// </para>
/// </summary>
public sealed class PerModuleDisableConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private const string LockKey = "Sync:PerModuleConcurrencyLock";

    /// <summary>
    /// Job parameter set when the worker skips due to a held lock — visible
    /// in the Hangfire dashboard's "Job parameters" panel for operators.
    /// </summary>
    public const string SkippedReasonParameter = "Sync:SkippedReason";

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

        if (TryAcquireOrSkip(
                filterContext.Connection,
                resource,
                TimeSpan.FromSeconds(TimeoutSeconds),
                out var distributedLock,
                out var skipReason))
        {
            filterContext.Items[LockKey] = distributedLock!;
        }
        else
        {
            // Skip-on-conflict — see class XML doc. Setting Canceled = true
            // tells Hangfire to NOT invoke the wrapped method and to treat the
            // job as Succeeded. No exception → no [AutomaticRetry] burst.
            filterContext.Canceled = true;
            filterContext.SetJobParameter(SkippedReasonParameter, skipReason);
        }
    }

    /// <summary>
    /// Bounded lock attempt. Returns <c>true</c> with the acquired lock when
    /// successful, <c>false</c> with a human-readable skip reason when the
    /// lock is already held by another worker. Extracted from
    /// <see cref="OnPerforming(PerformingContext)"/> so it can be exercised
    /// in isolation by stability tests without building a Hangfire
    /// <see cref="PerformingContext"/> fixture.
    /// </summary>
    public static bool TryAcquireOrSkip(
        IStorageConnection connection,
        string resource,
        TimeSpan timeout,
        out IDisposable? acquiredLock,
        out string? skipReason)
    {
        try
        {
            acquiredLock = connection.AcquireDistributedLock(resource, timeout);
            skipReason = null;
            return true;
        }
        catch (DistributedLockTimeoutException)
        {
            acquiredLock = null;
            skipReason =
                $"Another worker holds the lock '{resource}'. " +
                "Duplicate cron-fired execution skipped to avoid retry amplification.";
            return false;
        }
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
