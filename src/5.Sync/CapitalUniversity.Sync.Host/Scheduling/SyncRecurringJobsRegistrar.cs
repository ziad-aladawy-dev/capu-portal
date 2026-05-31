using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Scheduling;
using CapitalUniversity.Sync.Staff;
using CapitalUniversity.Sync.Student;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Host.Scheduling;

public sealed class SyncRecurringJobsRegistrar : IHostedService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<SyncRecurringJobsRegistrar> _logger;
    private readonly IOptions<SyncOptions> _options;
    private readonly IOptions<SyncRetentionOptions> _retentionOptions;
    private readonly IOptions<SyncOrphanReaperOptions> _reaperOptions;

    public SyncRecurringJobsRegistrar(
        IRecurringJobManager recurringJobManager,
        ILogger<SyncRecurringJobsRegistrar> logger,
        IOptions<SyncOptions> options,
        IOptions<SyncRetentionOptions> retentionOptions,
        IOptions<SyncOrphanReaperOptions> reaperOptions)
    {
        _recurringJobManager = recurringJobManager;
        _logger = logger;
        _options = options;
        _retentionOptions = retentionOptions;
        _reaperOptions = reaperOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var triggerQueue = _options.Value.DefaultQueue;

        _logger.LogInformation("Registering recurring sync jobs on queue {Queue}.", triggerQueue);

        // Drop legacy verification-only recurring entries that earlier versions
        // installed unconditionally. Idempotent no-op when not present.
        _recurringJobManager.RemoveIfExists("fake-sync-pull");
        _recurringJobManager.RemoveIfExists("fake-pipeline-pull");

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "student-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StudentSyncModule.Name, SyncDirection.Pull),
            cronExpression: Cron.Minutely());

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "student-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StudentSyncModule.Name, SyncDirection.Push),
            cronExpression: Cron.Minutely());

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "staff-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StaffSyncModule.Name, SyncDirection.Pull),
            cronExpression: Cron.Minutely());

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "staff-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StaffSyncModule.Name, SyncDirection.Push),
            cronExpression: Cron.Minutely());

        // Phase 9: retention sweeper. Always registered so its cron is observable in
        // the Hangfire dashboard; the service itself short-circuits if
        // Sync:Retention:Enabled = false (operator opt-in).
        _recurringJobManager.AddOrUpdate<SyncRetentionRecurringTrigger>(
            recurringJobId: "sync-retention",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(CancellationToken.None),
            cronExpression: _retentionOptions.Value.CronExpression);

        // Phase X hardening fix #5: orphan reaper. Wires the previously-unused
        // ISyncRunRepository.FindOrphanRunsAsync to a recurring sweeper.
        _recurringJobManager.AddOrUpdate<SyncOrphanReaperRecurringTrigger>(
            recurringJobId: "sync-orphan-reaper",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(CancellationToken.None),
            cronExpression: _reaperOptions.Value.CronExpression);

        _logger.LogInformation(
            "Recurring jobs registered: 'student-sync-pull', 'student-sync-push', 'staff-sync-pull', 'staff-sync-push', 'sync-retention', 'sync-orphan-reaper' (trigger queue: {Queue}; per-module dispatch queues resolved via Sync:ModuleQueues; retention enabled={RetentionEnabled} cron={RetentionCron}; reaper enabled={ReaperEnabled} cron={ReaperCron} grace={ReaperGrace}min).",
            triggerQueue,
            _retentionOptions.Value.Enabled,
            _retentionOptions.Value.CronExpression,
            _reaperOptions.Value.Enabled,
            _reaperOptions.Value.CronExpression,
            _reaperOptions.Value.GraceMinutes);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}