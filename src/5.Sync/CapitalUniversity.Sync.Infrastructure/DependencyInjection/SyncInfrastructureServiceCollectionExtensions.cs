using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Infrastructure.Alerting;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Dispatching;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Infrastructure.Observability;
using CapitalUniversity.Sync.Infrastructure.Pipeline;
using CapitalUniversity.Sync.Infrastructure.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CapitalUniversity.Sync.Infrastructure.DependencyInjection;

public static class SyncInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSyncInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SyncOptions>();

        services.AddSingleton<ISyncLogger, SyncLogger>();
        services.AddSingleton<ISyncModuleRegistry, SyncModuleRegistry>();
        services.AddSingleton<ISyncDispatcher, SyncDispatcher>();
        services.AddSingleton<SyncModuleExecutor>();

        // Ambient run-context accessor (AsyncLocal). The executor pins the active
        // SyncContext on it so scope-resolved components — e.g. writers that
        // dead-letter an unresolvable row — can read the run's CorrelationId /
        // Attempt without threading it through every pipeline signature.
        services.AddSingleton<SyncRunContextAccessor>();
        services.AddSingleton<ISyncRunContextAccessor>(sp => sp.GetRequiredService<SyncRunContextAccessor>());
        // Pipeline is a stateless singleton — per-run state lives inside its
        // RunAsync<,> locals; extractor/mapper/writer come from the request.
        services.AddSingleton<ISyncPipeline, SyncPipeline>();

        // Queue-config validator runs once at host startup (IHostedService) before
        // Hangfire workers dequeue. Fails fast on legacy ':' separators or dispatch
        // targets the Hangfire server isn't listening on.
        services.AddHostedService<SyncQueueConfigurationValidator>();

        // Phase 9: retention service + recurring trigger. The host registers the
        // recurring job; the trigger here is DI-resolved when Hangfire deserializes
        // the recurring method-call expression.
        services.AddOptions<SyncRetentionOptions>();
        services.AddSingleton<SyncRetentionService>();
        services.AddSingleton<SyncRetentionRecurringTrigger>();

        // Phase X hardening fix #5: orphan-run reaper. Wires the previously-unused
        // ISyncRunRepository.FindOrphanRunsAsync to a recurring sweep that marks
        // stranded Enqueued+null-JobId rows as Failed after a grace window.
        services.AddOptions<SyncOrphanReaperOptions>();
        services.AddSingleton<SyncOrphanReaperService>();
        services.AddSingleton<SyncOrphanReaperRecurringTrigger>();

        // Phase 10: observability surfaces. Queue-lag probe + alerting hook (default
        // implementation logs JSON-structured lines; operators replace via
        // AddSingleton<ISyncAlertingHook, MySlackHook>() to upgrade the destination).
        services.AddSingleton<QueueLagProbe>();
        services.TryAddSingleton<ISyncAlertingHook, LoggingSyncAlertingHook>();

        return services;
    }
}