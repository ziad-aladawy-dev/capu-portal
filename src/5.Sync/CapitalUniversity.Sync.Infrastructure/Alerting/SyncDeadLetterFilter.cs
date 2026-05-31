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

            // 1. Idempotent dead-letter row insert (HangfireJobId unique).
            var alreadyDeadLettered = db.DeadLetters.AsNoTracking()
                .Any(d => d.HangfireJobId == hangfireJobId);
            if (!alreadyDeadLettered)
            {
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
                catch (DbUpdateException dupEx)
                {
                    _logger.LogInformation(metadata.CorrelationId,
                        "Dead-letter row insert raced (unique-constraint violation). Treating as success. Error={Error}",
                        dupEx.Message);
                }
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

}