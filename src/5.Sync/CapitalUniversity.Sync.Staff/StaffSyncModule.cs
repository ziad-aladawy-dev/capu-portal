using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Staff.Configuration;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Pull;
using CapitalUniversity.Sync.Staff.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Staff;

/// <summary>
/// Second real domain module — proves multi-module isolation per Phase 7.
/// Delegates Pull/Push scaffolding to <see cref="SyncPipelineExtensions"/>.
/// Independent from Sync.Student: separate project, DbContext, schema, queue.
/// </summary>
public sealed class StaffSyncModule : ISyncModule
{
    public const string Name = "staff";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<StaffSyncOptions> _options;

    public StaffSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<StaffSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalStaff, StaffEntity>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: s => s.ExternalStaffId,
            extractorFactory: sp => sp.GetRequiredService<StaffExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<StaffMapper>(),
            validatorFactory: sp => sp.GetRequiredService<StaffValidator>(),
            writerFactory:    sp => sp.GetRequiredService<StaffWriter>(),
            cancellationToken);

    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPushAsync<StaffOutboxEntity, StaffOutboxDispatch>(
            _scopeFactory, context, _options.Value.PushBatchSize,
            externalKeySelector: r => r.Id.ToString(),
            extractorFactory: sp => sp.GetRequiredService<StaffOutboxExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<StaffOutboxMapper>(),
            validatorFactory: sp => sp.GetRequiredService<StaffOutboxValidator>(),
            writerFactory:    sp => sp.GetRequiredService<StaffOutboxWriter>(),
            cancellationToken);
}