using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Entities;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Sync.Infrastructure.Alerting;

/// <summary>
/// Detects terminal-failed transitions on <see cref="SyncModuleExecutor.ExecuteAsync"/> jobs
/// and writes a sync_dead_letters audit row + flips the sync_runs row to DeadLettered.
///
/// Hangfire transitions a job to <see cref="FailedState"/> after retries are exhausted
/// (with <see cref="Hangfire.AttemptsExceededAction.Fail"/>). This filter does not
/// implement retry or scheduling — it only observes Hangfire's terminal state.
///
/// <para>
/// <b>Idempotency contract.</b> Exactly one dead-letter row exists per Hangfire job.
/// The guarantee is anchored to the unique index <c>IX_dead_letters_HangfireJobId</c>
/// at the database, not to any in-process check: a duplicate INSERT (Hangfire's
/// double-FailedState artifact, two workers racing to mark the same job
/// terminal, etc.) is rejected by the database, surfaces as
/// <see cref="DbUpdateException"/> with a unique-violation inner exception
/// (SQL Server 2601/2627), and is treated by this filter as the idempotency
/// signal — the audit row is already there with the winner's metadata.
/// </para>
///
/// <para>
/// Audit writes use <see cref="SyncDbContext"/> directly with synchronous EF
/// Core methods (<c>SaveChanges</c>, <c>FirstOrDefault</c>) — Hangfire calls
/// the filter on a worker thread synchronously, so no async-over-sync bridge
/// is needed. The alerting hook is dispatched fire-and-forget on a thread-pool
/// task with a fresh DI scope (see <see cref="FireAndForgetAlert"/>).
/// </para>
/// </summary>
public sealed class SyncDeadLetterFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncLogger _logger;

    public SyncDeadLetterFilter(IServiceScopeFactory scopeFactory, ISyncLogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        Order = int.MaxValue;
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
        {
            return;
        }

        // Cancellations are not failures. They never become dead letters.
        if (failedState.Exception is OperationCanceledException)
        {
            return;
        }

        var job = context.BackgroundJob.Job;
        if (job is null)
        {
            return;
        }

        if (job.Type != typeof(SyncModuleExecutor) ||
            !string.Equals(job.Method.Name, nameof(SyncModuleExecutor.ExecuteAsync), StringComparison.Ordinal))
        {
            return;
        }

        if (job.Args.Count < 3)
        {
            return;
        }

        var moduleName = job.Args[0] as string ?? "<unknown>";
        var direction = job.Args[1] is SyncDirection d ? d : SyncDirection.Pull;
        var metadata = job.Args[2] as SyncRunMetadata;
        if (metadata is null)
        {
            return;
        }

        var hangfireJobId = context.BackgroundJob.Id;
        var lastError = failedState.Exception?.Message;
        var attemptedCount = ResolveAttemptCount(context);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

            // 1. One dead-letter row per Hangfire job. The unique index
            //    IX_dead_letters_HangfireJobId (added by migration
            //    20260601133451_AddUniqueIndexOnDeadLetterHangfireJobId) is the
            //    authoritative race-stopper: when Hangfire double-applies
            //    FailedState or two workers race to terminate the same job, the
            //    DB rejects the second INSERT and we observe a unique-constraint
            //    DbUpdateException — that is the idempotency signal, not an
            //    error. We do NOT pre-check with a SELECT: an unindexed exists
            //    query cannot close the race (both readers see "absent", both
            //    insert, both succeed without the constraint). Leaning on the
            //    constraint also saves one round-trip per terminal transition.
            db.DeadLetters.Add(new SyncDeadLetterEntity
            {
                CorrelationId = metadata.CorrelationId,
                HangfireJobId = hangfireJobId,
                ModuleName = moduleName,
                Direction = direction,
                AttemptedCount = attemptedCount,
                TerminalAt = DateTimeOffset.UtcNow,
                LastError = TextHelpers.Truncate(lastError, 4000)
            });

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException dupEx) when (IsUniqueConstraintViolation(dupEx))
            {
                // Lost the race — a peer worker already recorded the
                // dead-letter for this job. The audit row is already present
                // with the winner's metadata; nothing more to do here.
                _logger.LogInformation(metadata.CorrelationId,
                    "Dead-letter row already recorded by a concurrent worker (unique-constraint enforced). JobId={JobId}",
                    hangfireJobId);

                // Detach the rejected entity so the same DbContext can still
                // commit the run-state transition below without re-attempting
                // the failed insert.
                db.ChangeTracker.Clear();
            }

            // 2. Mark the run DeadLettered (Running → DeadLettered only; guards
            //    against the Hangfire double-FailedState artifact and any stale state).
            var run = db.Runs.FirstOrDefault(r => r.CorrelationId == metadata.CorrelationId);
            if (run is not null && run.Status == SyncRunStatus.Running)
            {
                run.Status = SyncRunStatus.DeadLettered;
                run.LastError = TextHelpers.Truncate(lastError, 4000);
                run.CompletedAt = DateTimeOffset.UtcNow;
                db.SaveChanges();

                // Run just transitioned to terminal-failed. Tell everyone who can
                // access the sync layer. Inside this guard so it fires exactly once
                // per dead-letter (the Running→DeadLettered flip is the idempotency
                // anchor), fire-and-forget so the Hangfire worker thread isn't held.
                FireAndForgetOutcomeNotification(metadata.CorrelationId, new SyncOutcomeNotice(
                    metadata.CorrelationId,
                    moduleName,
                    direction,
                    Success: false,
                    RecordsProcessed: 0,
                    RecordsFailed: 0,
                    Error: lastError));
            }

            _logger.LogWarning(metadata.CorrelationId,
                "Sync job dead-lettered. Module={ModuleName} Direction={Direction} JobId={JobId} AttemptedCount={AttemptedCount} LastError={LastError}",
                moduleName, direction, hangfireJobId, attemptedCount, lastError);

            // Phase X.2 fix #1: alerting hook fan-out is now TRUE fire-and-forget.
            // The previous .GetAwaiter().GetResult() blocked the Hangfire worker
            // thread on the hook's async I/O. Task.Run + fresh DI scope decouples
            // the alert dispatch from the worker thread entirely. The alerting
            // hook's exceptions are caught + logged inside the background task so
            // a flaky destination cannot affect dead-letter recording (which
            // already committed synchronously above).
            FireAndForgetAlert(metadata.CorrelationId, hangfireJobId, new SyncAlert
            {
                CorrelationId = metadata.CorrelationId,
                ModuleName = moduleName,
                Direction = direction.ToString(),
                Title = $"Sync dead-letter: {moduleName} {direction}",
                Severity = "Critical",
                Detail = lastError,
                HangfireJobId = hangfireJobId,
                AttemptCount = attemptedCount,
                Tags = metadata.Tags
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(metadata.CorrelationId, ex,
                "Failed to write dead-letter audit for JobId={JobId}.", hangfireJobId);
        }
    }

    /// <summary>
    /// Resolves a fresh DI scope on a thread-pool thread (the per-event scope used
    /// for the audit writes is already disposed by the time this awaits), invokes
    /// the alerting hook, and swallows + logs any exception. Never blocks the
    /// caller. Matches the fire-and-forget contract documented on
    /// <see cref="ISyncAlertingHook"/>.
    /// </summary>
    private void FireAndForgetAlert(Guid correlationId, string hangfireJobId, SyncAlert alert)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var alertScope = _scopeFactory.CreateAsyncScope();
                var hook = alertScope.ServiceProvider.GetService<ISyncAlertingHook>();
                if (hook is null) return;
                await hook.DeadLetterAsync(alert, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception alertEx)
            {
                _logger.LogWarning(correlationId,
                    "Dead-letter alerting hook failed (audit row was still written). JobId={JobId} Error={Error}",
                    hangfireJobId, alertEx.Message);
            }
        });
    }

    /// <summary>
    /// Resolves a fresh DI scope on a thread-pool thread and fans out the
    /// terminal-failure notification to every sync-permission holder, swallowing +
    /// logging any failure. Mirrors <see cref="FireAndForgetAlert"/>: the dead-letter
    /// audit row is already committed synchronously above, so notification dispatch
    /// must never block or fail the worker thread. No-op when no notifier is
    /// registered.
    /// </summary>
    private void FireAndForgetOutcomeNotification(Guid correlationId, SyncOutcomeNotice notice)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetService<ISyncOutcomeNotifier>();
                if (notifier is null) return;
                await notifier.NotifyAsync(notice, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(correlationId,
                    "Dead-letter notification failed (audit row was still written). Error={Error}",
                    ex.Message);
            }
        });
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // No action: the filter only records terminal state, never reverses it.
    }

    private static int ResolveAttemptCount(ApplyStateContext context)
    {
        var retryCount = context.Connection.GetJobParameter(context.BackgroundJob.Id, "RetryCount");
        if (int.TryParse(retryCount, out var n))
        {
            return n + 1;
        }
        return 1;
    }

    /// <summary>
    /// Inspects a <see cref="DbUpdateException"/> for the unique-constraint
    /// violation that signals a peer worker has already recorded the dead-letter.
    /// SQL Server raises 2601 (duplicate key in a unique index) or 2627 (unique
    /// constraint violation); both are inspected via reflection because EF Core
    /// hides the provider type behind <c>DbUpdateException.InnerException</c>
    /// and we don't want a hard reference to Microsoft.Data.SqlClient here.
    /// Returns true on either code. Returns true for relational providers that
    /// surface generic unique-violation strings too, so SQLite-backed tests
    /// observe the same idempotency path as production SQL Server.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            // SQL Server: SqlException.Number ∈ {2601, 2627}
            var numberProp = inner.GetType().GetProperty("Number");
            if (numberProp?.GetValue(inner) is int code && (code == 2601 || code == 2627))
            {
                return true;
            }

            // SQLite / other relational providers expose the violation through
            // the message text (no portable typed property exists).
            var message = inner.Message;
            if (!string.IsNullOrEmpty(message) &&
                (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("IX_dead_letters_HangfireJobId", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}