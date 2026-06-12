using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Courses;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Sync.Infrastructure.Scheduling;
using CapitalUniversity.Sync.Registration;
using CapitalUniversity.Sync.Schedules;
using CapitalUniversity.Sync.Staff;
using CapitalUniversity.Sync.Student;
using Hangfire;
// Sync.Finance removed (Treasury is the source of truth for fees).
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
    private readonly IOptions<TreasuryOptions> _treasuryOptions;

    public SyncRecurringJobsRegistrar(
        IRecurringJobManager recurringJobManager,
        ILogger<SyncRecurringJobsRegistrar> logger,
        IOptions<SyncOptions> options,
        IOptions<SyncRetentionOptions> retentionOptions,
        IOptions<SyncOrphanReaperOptions> reaperOptions,
        IOptions<TreasuryOptions> treasuryOptions)
    {
        _recurringJobManager = recurringJobManager;
        _logger = logger;
        _options = options;
        _retentionOptions = retentionOptions;
        _reaperOptions = reaperOptions;
        _treasuryOptions = treasuryOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var triggerQueue = _options.Value.DefaultQueue;
        var defaultCron = _options.Value.DefaultCronExpression;

        _logger.LogInformation("Registering recurring sync jobs on queue {Queue} with cadence {Cron}.", triggerQueue, defaultCron);

        // Drop legacy verification-only recurring entries that earlier versions
        // installed unconditionally. Idempotent no-op when not present.
        _recurringJobManager.RemoveIfExists("fake-sync-pull");
        _recurringJobManager.RemoveIfExists("fake-pipeline-pull");

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "student-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StudentSyncModule.Name, SyncDirection.Pull),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "student-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StudentSyncModule.Name, SyncDirection.Push),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "staff-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StaffSyncModule.Name, SyncDirection.Pull),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "staff-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(StaffSyncModule.Name, SyncDirection.Push),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "courses-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(CoursesSyncModule.Name, SyncDirection.Pull),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "courses-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(CoursesSyncModule.Name, SyncDirection.Push),
            cronExpression: defaultCron);

        // finance-sync removed (Treasury owns fees). Drop any previously
        // installed finance recurring entries so they stop firing.
        _recurringJobManager.RemoveIfExists("finance-sync-pull");
        _recurringJobManager.RemoveIfExists("finance-sync-push");

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "schedules-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(SchedulesSyncModule.Name, SyncDirection.Pull),
            cronExpression: defaultCron);

        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "schedules-sync-push",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(SchedulesSyncModule.Name, SyncDirection.Push),
            cronExpression: defaultCron);

        // Registration is pull-only — the portal never originates registration
        // changes, so there is no matching '-push' job. Drop any push entry a
        // prior build may have installed.
        _recurringJobManager.AddOrUpdate<SyncRecurringTrigger>(
            recurringJobId: "registration-sync-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.TriggerAsync(RegistrationSyncModule.Name, SyncDirection.Pull),
            cronExpression: defaultCron);
        _recurringJobManager.RemoveIfExists("registration-sync-push");

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

        // Treasury receipt pull (Phase 3). Pulls ConnectionTypeId=6 receipts
        // into Core via ICoreWriteGateway on the configured cron.
        _recurringJobManager.AddOrUpdate<TreasuryReceiptPullTrigger>(
            recurringJobId: "treasury-receipt-pull",
            queue: triggerQueue,
            methodCall: trigger => trigger.RunAsync(CancellationToken.None),
            cronExpression: _treasuryOptions.Value.ReceiptPullCron);

        // Treasury reconciliation (Phase 7). Polls PendingPayment orders and
        // settles/expires them via the shared idempotent settlement path.
        _recurringJobManager.AddOrUpdate<TreasuryReconciliationTrigger>(
            recurringJobId: "treasury-reconciliation",
            queue: triggerQueue,
            methodCall: trigger => trigger.RunAsync(CancellationToken.None),
            cronExpression: _treasuryOptions.Value.ReconciliationCron);

        _logger.LogInformation(
            "Recurring jobs registered: 'student-sync-pull', 'student-sync-push', 'staff-sync-pull', 'staff-sync-push', 'courses-sync-pull', 'courses-sync-push', 'finance-sync-pull', 'finance-sync-push', 'schedules-sync-pull', 'schedules-sync-push', 'sync-retention', 'sync-orphan-reaper' (trigger queue: {Queue}; per-module dispatch queues resolved via Sync:ModuleQueues; retention enabled={RetentionEnabled} cron={RetentionCron}; reaper enabled={ReaperEnabled} cron={ReaperCron} grace={ReaperGrace}min).",
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