using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Execution;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Dispatching;

/// <summary>
/// Thin enqueue wrapper. Owns no execution, no retry, no scheduling, no state.
///
/// Failure-safe flow:
///   1. Open sync.runs (Status = Enqueued, HangfireJobId = null).
///      Audit-write failure is logged but does not block enqueue.
///   2. Try Hangfire enqueue.
///      Enqueue success → link Hangfire job id back (idempotent).
///      Enqueue failure → mark run as Failed (Enqueued → Failed) and rethrow.
///   3. No run can remain stuck in Enqueued without a Hangfire job AND without operator visibility.
/// </summary>
public sealed class SyncDispatcher : ISyncDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ISyncLogger _logger;
    private readonly IOptionsMonitor<SyncOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public SyncDispatcher(
        IBackgroundJobClient backgroundJobClient,
        ISyncLogger logger,
        IOptionsMonitor<SyncOptions> options,
        IServiceScopeFactory scopeFactory)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
        _options = options;
        _scopeFactory = scopeFactory;
    }

    public async Task<string> DispatchAsync(
        string moduleName,
        SyncDirection direction,
        SyncRunMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name must be provided.", nameof(moduleName));
        }

        ArgumentNullException.ThrowIfNull(metadata);

        cancellationToken.ThrowIfCancellationRequested();

        var queue = _options.CurrentValue.ResolveQueue(moduleName, direction);
        var enqueuedAt = DateTimeOffset.UtcNow;

        // 1. Open the audit row. Failure here is logged but doesn't block enqueue.
        await TryAuditAsync(
            metadata.CorrelationId,
            "OpenRun",
            async repo => await repo.OpenRunAsync(new SyncRunRecord
            {
                CorrelationId = metadata.CorrelationId,
                ModuleName = moduleName,
                Direction = direction,
                TriggeredBy = metadata.TriggeredBy,
                Queue = queue,
                EnqueuedAt = enqueuedAt,
                Status = SyncRunStatus.Enqueued
            }, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        // 2. Try Hangfire enqueue. Failure here is terminal for the run.
        var job = Job.FromExpression<SyncModuleExecutor>(
            executor => executor.ExecuteAsync(
                moduleName,
                direction,
                metadata,
                null!,
                JobCancellationToken.Null));

        string jobId;
        try
        {
            jobId = _backgroundJobClient.Create(job, new EnqueuedState(queue));
        }
        catch (Exception enqueueEx)
        {
            _logger.LogError(metadata.CorrelationId, enqueueEx,
                "Hangfire enqueue failed. Module={ModuleName} Direction={Direction} Queue={Queue}",
                moduleName, direction, queue);

            // Mark run Failed so no row stays stuck in Enqueued without a job.
            await TryAuditAsync(
                metadata.CorrelationId,
                "MarkFailed",
                async repo => await repo.MarkFailedAsync(
                    metadata.CorrelationId,
                    $"Hangfire enqueue failed: {enqueueEx.Message}",
                    cancellationToken).ConfigureAwait(false))
                .ConfigureAwait(false);

            throw;
        }

        // 3. Link Hangfire job id back to the audit run. Idempotent and warn-only.
        await TryAuditAsync(
            metadata.CorrelationId,
            "LinkHangfireJob",
            async repo => await repo.LinkHangfireJobAsync(
                metadata.CorrelationId, jobId, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        _logger.LogInformation(metadata.CorrelationId,
            "Sync job enqueued. Module={ModuleName} Direction={Direction} TriggeredBy={TriggeredBy} Queue={Queue} JobId={JobId}",
            moduleName, direction, metadata.TriggeredBy, queue, jobId);

        return jobId;
    }

    private async Task TryAuditAsync(
        Guid correlationId,
        string operation,
        Func<ISyncRunRepository, Task> action)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISyncRunRepository>();
            await action(repo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit writes must not break the dispatch path. Hangfire is the source of truth.
            _logger.LogWarning(correlationId,
                "Sync audit write failed during {Operation}. Error={Error}",
                operation, ex.Message);
        }
    }
}