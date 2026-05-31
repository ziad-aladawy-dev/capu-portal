using System.Diagnostics;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Locking;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;

namespace CapitalUniversity.Sync.Infrastructure.Execution;

public sealed class SyncModuleExecutor
{
    /// <summary>
    /// Bounded timeout for cancellation-cleanup writes (e.g. <c>MarkCancelledAsync</c>).
    /// Prevents host shutdown from hanging on a slow DB while we try to record the
    /// cancellation in the audit table.
    /// </summary>
    public static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(15);

    private readonly ISyncModuleRegistry _registry;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncAlertingHook? _alertingHook;

    public SyncModuleExecutor(
        ISyncModuleRegistry registry,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        ISyncAlertingHook? alertingHook = null)
    {
        _registry = registry;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _alertingHook = alertingHook;
    }

    // Lock timeout intentionally short. Hangfire re-fetches a job after
    // SlidingInvisibilityTimeout (5 min) and retry-launches it; if the original
    // worker still holds the lock, the duplicate worker fails fast here and
    // hands off to AutomaticRetry instead of blocking a worker thread.
    [PerModuleDisableConcurrency(timeoutSeconds: 30)]
    [AutomaticRetry(
        Attempts = 4,
        DelaysInSeconds = new[] { 60, 300, 900, 3600 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(
        string moduleName,
        SyncDirection direction,
        SyncRunMetadata metadata,
        PerformContext? performContext,
        IJobCancellationToken jobCancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(jobCancellationToken);

        var cancellationToken = jobCancellationToken.ShutdownToken;
        var attempt = ResolveAttempt(performContext);
        var hangfireJobId = performContext?.BackgroundJob?.Id;
        var startedAt = DateTimeOffset.UtcNow;

        using var scope = _logger.BeginCorrelationScope(metadata.CorrelationId);

        _logger.LogInformation(
            metadata.CorrelationId,
            "Sync execution started. Module={ModuleName} Direction={Direction} TriggeredBy={TriggeredBy} Attempt={Attempt}",
            moduleName, direction, metadata.TriggeredBy, attempt);

        // Phase X hardening fix #11: self-heal. The dispatcher's OpenRunAsync may have
        // failed silently (audit-DB blip → no sync.runs row). Without a row, every
        // subsequent Mark* call silently skips and the run is invisible in audit.
        // OpenRunAsync is now idempotent — calling it defensively here either creates
        // the row from job context or is a no-op when the dispatcher already created
        // it. Combined with fix #4 (alerting on audit failures), no run ever goes
        // completely dark.
        await UpdateRunAsync(
            r => r.OpenRunAsync(new SyncRunRecord
            {
                CorrelationId = metadata.CorrelationId,
                ModuleName = moduleName,
                Direction = direction,
                TriggeredBy = metadata.TriggeredBy ?? "<unknown>",
                Queue = performContext?.GetJobParameter<string>("Queue") ?? "<unknown>",
                EnqueuedAt = performContext?.BackgroundJob?.CreatedAt is DateTime created
                    ? new DateTimeOffset(DateTime.SpecifyKind(created, DateTimeKind.Utc))
                    : startedAt,
                Status = SyncRunStatus.Enqueued
            }, cancellationToken),
            metadata.CorrelationId,
            "OpenRun(self-heal)").ConfigureAwait(false);

        await UpdateRunAsync(
            r => r.MarkStartedAsync(metadata.CorrelationId, attempt, startedAt, cancellationToken),
            metadata.CorrelationId,
            "MarkStarted").ConfigureAwait(false);

        ISyncModule module;
        try
        {
            module = _registry.Resolve(moduleName);
        }
        catch (SyncModuleNotFoundException ex)
        {
            await RecordFailureAsync(
                metadata.CorrelationId, hangfireJobId, attempt,
                ex, "Module not found", cancellationToken).ConfigureAwait(false);

            _logger.LogError(metadata.CorrelationId, ex,
                "Sync module not found. Module={ModuleName}", moduleName);
            throw;
        }

        var context = new SyncContext
        {
            ModuleName = moduleName,
            Direction = direction,
            Metadata = metadata,
            Attempt = attempt
        };

        var stopwatch = Stopwatch.StartNew();
        SyncResult result;

        try
        {
            result = direction switch
            {
                SyncDirection.Pull => await module.PullAsync(context, cancellationToken).ConfigureAwait(false),
                SyncDirection.Push => await module.PushAsync(context, cancellationToken).ConfigureAwait(false),
                _ => throw new SyncExecutionException(
                    $"Unknown sync direction: {direction}", metadata.CorrelationId, moduleName)
            };
        }
        catch (OperationCanceledException oce)
            when (cancellationToken.IsCancellationRequested || oce.CancellationToken.IsCancellationRequested)
        {
            // Legitimate cancellation: either the worker's shutdown token is signaled
            // (host stopping) or the OCE itself carries a signaled token (e.g. a module-
            // supplied source token from the CancellationCoordinator).
            stopwatch.Stop();

            // Bounded-timeout cleanup: a slow DB must not hold up shutdown.
            using var cleanupCts = new CancellationTokenSource(CleanupTimeout);
            await UpdateRunAsync(
                r => r.MarkCancelledAsync(metadata.CorrelationId, cleanupCts.Token),
                metadata.CorrelationId,
                "MarkCancelled").ConfigureAwait(false);

            _logger.LogInformation(metadata.CorrelationId,
                "Sync execution cancelled. Module={ModuleName} Direction={Direction} Elapsed={Elapsed}",
                moduleName, direction, stopwatch.Elapsed);

            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Spurious OCE — neither the worker token nor the exception's token is
            // signaled. The module likely used OCE for non-cancellation control flow.
            // Treat as a regular failure so the audit row is `Failed`, not `Cancelled`.
            stopwatch.Stop();

            _logger.LogWarning(metadata.CorrelationId,
                "Sync execution caught spurious OperationCanceledException. Module={ModuleName} Direction={Direction} Attempt={Attempt} Elapsed={Elapsed}. " +
                "Neither the worker shutdown token nor the exception's own token is signaled — treating as a module-level failure.",
                moduleName, direction, attempt, stopwatch.Elapsed);

            await RecordFailureAsync(
                metadata.CorrelationId, hangfireJobId, attempt,
                ex, "Spurious OCE without cancellation signal: " + ex.Message,
                cancellationToken).ConfigureAwait(false);

            throw new SyncExecutionException(
                $"Sync module '{moduleName}' threw a spurious OperationCanceledException without any cancellation signal.",
                ex, metadata.CorrelationId, moduleName);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await RecordFailureAsync(
                metadata.CorrelationId, hangfireJobId, attempt,
                ex, ex.Message, cancellationToken).ConfigureAwait(false);

            _logger.LogError(metadata.CorrelationId, ex,
                "Sync execution threw. Module={ModuleName} Direction={Direction} Attempt={Attempt} Elapsed={Elapsed}",
                moduleName, direction, attempt, stopwatch.Elapsed);

            throw new SyncExecutionException(
                $"Sync module '{moduleName}' threw during {direction}.",
                ex, metadata.CorrelationId, moduleName);
        }
        finally
        {
            stopwatch.Stop();
        }

        if (result.Success)
        {
            await UpdateRunAsync(
                r => r.MarkSucceededAsync(
                    metadata.CorrelationId,
                    result.RecordsProcessed,
                    result.RecordsFailed,
                    result.Duration,
                    cancellationToken),
                metadata.CorrelationId,
                "MarkSucceeded").ConfigureAwait(false);

            _logger.LogInformation(metadata.CorrelationId,
                "Sync execution succeeded. Module={ModuleName} Direction={Direction} Processed={Processed} Failed={Failed} Duration={Duration}",
                moduleName, direction, result.RecordsProcessed, result.RecordsFailed, result.Duration);
        }
        else
        {
            var errorMessage = result.ErrorMessage ?? "Sync module reported failure.";

            await RecordFailureAsync(
                metadata.CorrelationId, hangfireJobId, attempt,
                exception: null, errorMessage, cancellationToken).ConfigureAwait(false);

            _logger.LogError(metadata.CorrelationId, null,
                "Sync execution reported failure. Module={ModuleName} Direction={Direction} Attempt={Attempt} Processed={Processed} Failed={Failed} Duration={Duration} Error={Error}",
                moduleName, direction, attempt, result.RecordsProcessed, result.RecordsFailed, result.Duration, errorMessage);

            throw new SyncExecutionException(errorMessage, metadata.CorrelationId, moduleName);
        }
    }

    private static int ResolveAttempt(PerformContext? performContext)
    {
        if (performContext is null)
        {
            return 1;
        }

        var retryCount = performContext.GetJobParameter<int?>("RetryCount") ?? 0;
        return retryCount + 1;
    }

    private async Task UpdateRunAsync(
        Func<ISyncRunRepository, Task> action,
        Guid correlationId,
        string operation)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISyncRunRepository>();
            await action(repo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Phase X hardening fix #4: audit write failures used to log a Warning
            // and disappear. That made a flapping audit DB silently strand runs.
            // Escalated to Error + alerting-hook fan-out so operators see the
            // problem immediately. The executor's main flow still proceeds —
            // Hangfire remains the source of truth for job state, audit is best-effort.
            _logger.LogError(correlationId, ex,
                "Sync audit write FAILED during {Operation}. The run may have an incomplete audit trail. Error={Error}",
                operation, ex.Message);

            try
            {
                if (_alertingHook is not null)
                {
                    await _alertingHook.PipelineFailureAsync(new SyncAlert
                    {
                        CorrelationId = correlationId,
                        ModuleName = "<audit>",
                        Direction = "<n/a>",
                        Title = $"Sync audit write failed: {operation}",
                        Severity = "Warning",
                        Detail = ex.Message
                    }, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception alertEx)
            {
                _logger.LogWarning(correlationId,
                    "Audit-failure alerting hook also threw. CorrelationId={CorrelationId} Error={Error}",
                    correlationId, alertEx.Message);
            }
        }
    }

    /// <summary>
    /// Records a per-attempt failure into sync.failures. Does NOT change sync.runs status —
    /// the run stays Running across Hangfire retries. Terminal transitions are owned by:
    ///   • <c>SyncDispatcher</c> (Enqueued → Failed on enqueue exception)
    ///   • <c>SyncDeadLetterFilter</c> (Running → DeadLettered on Hangfire terminal failed state)
    /// </summary>
    private async Task RecordFailureAsync(
        Guid correlationId,
        string? hangfireJobId,
        int attempt,
        Exception? exception,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var failureRepo = scope.ServiceProvider.GetRequiredService<IFailureRepository>();

            await failureRepo.RecordAsync(new SyncFailureRecord
            {
                CorrelationId = correlationId,
                HangfireJobId = hangfireJobId,
                Attempt = attempt,
                ErrorType = exception?.GetType().FullName ?? "ModuleReportedFailure",
                ErrorMessage = errorMessage,
                StackTrace = exception?.StackTrace
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(correlationId,
                "Sync failure audit write failed. Error={Error}",
                ex.Message);
        }
    }
}