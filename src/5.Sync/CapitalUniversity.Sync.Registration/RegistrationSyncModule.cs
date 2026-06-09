using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Registration.Configuration;
using CapitalUniversity.Sync.Registration.Domain;
using CapitalUniversity.Sync.Registration.Pull;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Registration;

/// <summary>
/// Registration sync — pulls <see cref="ExternalRegistration"/> snapshots into
/// Core's <c>StudentRegisteredCourse</c> read-model. Delegates the Pull
/// scaffolding to <see cref="SyncPipelineExtensions"/>, exactly like the other
/// modules.
///
/// <para>
/// <b>Pull-only.</b> The portal never registers, drops, or modifies
/// enrollments, so there is nothing to push upstream. <see cref="PushAsync"/> is
/// a deliberate no-op (no outbox, no DbContext), and the host registers only a
/// <c>registration-sync-pull</c> recurring job. Keeping the no-op here — rather
/// than throwing — means an accidental Push trigger is a harmless success
/// instead of a dead-lettered run.
/// </para>
/// </summary>
public sealed class RegistrationSyncModule : ISyncModule
{
    public const string Name = "registration";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RegistrationSyncOptions> _options;

    public RegistrationSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<RegistrationSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalRegistration, RegistrationSyncDispatch>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: r => r.ExternalRegistrationId,
            extractorFactory: sp => sp.GetRequiredService<RegistrationExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<RegistrationMapper>(),
            validatorFactory: sp => sp.GetRequiredService<RegistrationValidator>(),
            writerFactory:    sp => sp.GetRequiredService<RegistrationWriter>(),
            cancellationToken);

    /// <summary>
    /// No-op: registrations are read-only inbound, so there is no outbound
    /// queue to drain. Returns an immediate empty success.
    /// </summary>
    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => Task.FromResult(SyncResult.Ok(processed: 0, duration: TimeSpan.Zero));
}
