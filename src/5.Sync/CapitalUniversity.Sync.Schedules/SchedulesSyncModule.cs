using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Schedules.Configuration;
using CapitalUniversity.Sync.Schedules.Domain;
using CapitalUniversity.Sync.Schedules.Pull;
using CapitalUniversity.Sync.Schedules.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Schedules;

/// <summary>
/// Schedules sync — syncs <see cref="ExternalScheduleSlot"/> rows (mirrors Core
/// <c>Modules.Schedule.Domain.ScheduleSlot</c>).
/// </summary>
public sealed class SchedulesSyncModule : ISyncModule
{
    public const string Name = "schedules";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SchedulesSyncOptions> _options;

    public SchedulesSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulesSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalScheduleSlot, ScheduleSlotSyncDispatch>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: s => s.ExternalScheduleSlotId,
            extractorFactory: sp => sp.GetRequiredService<ScheduleSlotExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<ScheduleSlotMapper>(),
            validatorFactory: sp => sp.GetRequiredService<ScheduleSlotValidator>(),
            writerFactory:    sp => sp.GetRequiredService<ScheduleSlotWriter>(),
            cancellationToken);

    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPushAsync<ScheduleSlotOutboxEntity, ScheduleSlotOutboxDispatch>(
            _scopeFactory, context, _options.Value.PushBatchSize,
            externalKeySelector: r => r.Id.ToString(),
            extractorFactory: sp => sp.GetRequiredService<ScheduleSlotOutboxExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<ScheduleSlotOutboxMapper>(),
            validatorFactory: sp => sp.GetRequiredService<ScheduleSlotOutboxValidator>(),
            writerFactory:    sp => sp.GetRequiredService<ScheduleSlotOutboxWriter>(),
            cancellationToken);
}
