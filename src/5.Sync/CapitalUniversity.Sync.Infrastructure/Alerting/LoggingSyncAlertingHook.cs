using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;

namespace CapitalUniversity.Sync.Infrastructure.Alerting;

/// <summary>
/// Default <see cref="ISyncAlertingHook"/> — writes a structured-log line per alert
/// at <c>Information</c> for dead-letters and <c>Warning</c> for pipeline failures.
/// Operators get baseline observability for free; replacing the registration with a
/// Slack/PagerDuty/webhook implementation upgrades the destination without touching
/// the call sites.
/// </summary>
public sealed class LoggingSyncAlertingHook : ISyncAlertingHook
{
    private readonly ISyncLogger _logger;

    public LoggingSyncAlertingHook(ISyncLogger logger)
    {
        _logger = logger;
    }

    public Task DeadLetterAsync(SyncAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _logger.LogInformation(alert.CorrelationId,
            "[ALERT][DeadLetter] Module={Module} Direction={Direction} Title={Title} Severity={Severity} JobId={JobId} AttemptCount={AttemptCount} Detail={Detail}",
            alert.ModuleName,
            alert.Direction,
            alert.Title,
            alert.Severity,
            alert.HangfireJobId,
            alert.AttemptCount,
            alert.Detail);
        return Task.CompletedTask;
    }

    public Task PipelineFailureAsync(SyncAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _logger.LogWarning(alert.CorrelationId,
            "[ALERT][PipelineFailure] Module={Module} Direction={Direction} Title={Title} Severity={Severity} JobId={JobId} AttemptCount={AttemptCount} Detail={Detail}",
            alert.ModuleName,
            alert.Direction,
            alert.Title,
            alert.Severity,
            alert.HangfireJobId,
            alert.AttemptCount,
            alert.Detail);
        return Task.CompletedTask;
    }
}