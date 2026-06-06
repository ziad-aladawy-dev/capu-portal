using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Courses.Configuration;
using CapitalUniversity.Sync.Courses.Domain;
using CapitalUniversity.Sync.Courses.Pull;
using CapitalUniversity.Sync.Courses.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Courses;

/// <summary>
/// Third real domain module — proves multi-module isolation extends to a
/// catalog-shape domain. Delegates Pull/Push scaffolding to
/// <see cref="SyncPipelineExtensions"/>. Independent from Sync.Student and
/// Sync.Staff: separate project, DbContext, schema, queue.
/// </summary>
public sealed class CoursesSyncModule : ISyncModule
{
    public const string Name = "courses";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CoursesSyncOptions> _options;

    public CoursesSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<CoursesSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalCourse, Course>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: c => c.ExternalCourseId,
            extractorFactory: sp => sp.GetRequiredService<CourseExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<CourseMapper>(),
            validatorFactory: sp => sp.GetRequiredService<CourseValidator>(),
            writerFactory:    sp => sp.GetRequiredService<CourseWriter>(),
            cancellationToken);

    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPushAsync<CourseOutboxEntity, CourseOutboxDispatch>(
            _scopeFactory, context, _options.Value.PushBatchSize,
            externalKeySelector: r => r.Id.ToString(),
            extractorFactory: sp => sp.GetRequiredService<CourseOutboxExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<CourseOutboxMapper>(),
            validatorFactory: sp => sp.GetRequiredService<CourseOutboxValidator>(),
            writerFactory:    sp => sp.GetRequiredService<CourseOutboxWriter>(),
            cancellationToken);
}
