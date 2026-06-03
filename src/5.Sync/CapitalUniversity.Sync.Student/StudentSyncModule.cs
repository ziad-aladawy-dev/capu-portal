using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Configuration;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Pull;
using CapitalUniversity.Sync.Student.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// See StudentMapper.cs — Sync.Student namespace collides with Core's Student type.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Student;

/// <summary>
/// First real domain module. Delegates the Pull/Push scaffolding to
/// <see cref="SyncPipelineExtensions"/> and supplies only the bits that genuinely
/// vary per domain — module name, batch size, key selector, and per-stage
/// component factories. Cursor advance is handled by the helper through
/// <see cref="ICursorObserver"/> on <see cref="StudentExtractor"/>.
///
/// Module is infrastructure-isolated — references only <c>Sync.Abstractions</c>
/// at the project level. Hangfire/Sync.Persistence are not visible here.
/// </summary>
public sealed class StudentSyncModule : ISyncModule
{
    public const string Name = "students";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<StudentSyncOptions> _options;

    public StudentSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<StudentSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalStudent, CoreStudent>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: s => s.ExternalStudentId,
            extractorFactory: sp => sp.GetRequiredService<StudentExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<StudentMapper>(),
            validatorFactory: sp => sp.GetRequiredService<StudentValidator>(),
            writerFactory:    sp => sp.GetRequiredService<StudentWriter>(),
            cancellationToken);

    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPushAsync<StudentOutboxEntity, StudentOutboxDispatch>(
            _scopeFactory, context, _options.Value.PushBatchSize,
            // Per-row merge key. OutboxId-based so distinct outbox rows for the
            // same ExternalStudentId (sequential updates) all flow through; sink
            // idempotency handles any re-presented payload safely.
            externalKeySelector: r => r.Id.ToString(),
            extractorFactory: sp => sp.GetRequiredService<StudentOutboxExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<StudentOutboxMapper>(),
            validatorFactory: sp => sp.GetRequiredService<StudentOutboxValidator>(),
            writerFactory:    sp => sp.GetRequiredService<StudentOutboxWriter>(),
            cancellationToken);
}