using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Dispatching;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Infrastructure.Pipeline;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Repositories;
using CapitalUniversity.Sync.Student;
using CapitalUniversity.Sync.Student.Configuration;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Pull;
using CapitalUniversity.Sync.Student.Push;
using CapitalUniversity.Sync.Student.Sources;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// `Student` namespace shadows the type; alias to access the Core entity.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// End-to-end sanity check on the Student pull pipeline. After the staging-
/// table refactor the writer no longer persists into a sync-side DbContext —
/// it forwards to <see cref="ICoreWriteGateway"/>. We mock the gateway and
/// assert two things only:
/// <list type="number">
///   <item>The audit row in sync.runs records the correct processed / failed counts (48 / 2: the InMemoryExternalStudentSource emits 50; rows #10 and #20 have empty emails and fall to the validator).</item>
///   <item>The pipeline reached the gateway with 48 students (the validator-passing remainder).</item>
/// </list>
/// Database-side merge behavior (insert vs update, external-wins, FK resolution)
/// is the gateway's responsibility and is tested separately in the Core suite.
/// </summary>
public class SyncIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SyncDbContext _syncDb;
    private readonly Mock<ICoreWriteGateway> _gatewayMock;
    private readonly List<CoreStudent> _capturedStudents = new();
    private readonly string _syncDbName = "SyncDb_" + Guid.NewGuid();
    private readonly string _studentDbName = "StudentDb_" + Guid.NewGuid();

    public SyncIntegrationTests()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sync:Student:ConnectionString"] = "Server=none",
                ["Sync:Student:BatchSize"] = "10"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        services.AddDbContext<SyncDbContext>(o => o.UseInMemoryDatabase(_syncDbName));
        // StudentSyncDbContext is still registered because the push pipeline
        // resolves it via DI even when this test only exercises pull. Its
        // tables are unused here.
        services.AddDbContext<StudentSyncDbContext>(o => o.UseInMemoryDatabase(_studentDbName));

        // CoreWriteGateway mock — captures the batch the writer hands over, so
        // we can assert the pipeline reached the gateway with the right number
        // of validator-passing rows. Returns Persisted = batch.Count so the
        // pipeline records the same count in the audit row.
        _gatewayMock = new Mock<ICoreWriteGateway>();
        _gatewayMock
            .Setup(g => g.UpsertAsync(
                It.IsAny<IReadOnlyList<CoreStudent>>(),
                It.IsAny<Action<CoreStudent, CoreStudent>>(),
                It.IsAny<CoreUpsertOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CoreStudent>, Action<CoreStudent, CoreStudent>, CoreUpsertOptions, CancellationToken>(
                (batch, _, _, _) => _capturedStudents.AddRange(batch))
            .ReturnsAsync((IReadOnlyList<CoreStudent> batch, Action<CoreStudent, CoreStudent> _, CoreUpsertOptions __, CancellationToken ___)
                => new CoreUpsertResult { Persisted = batch.Count });
        services.AddSingleton(_gatewayMock.Object);

        services.Configure<StudentSyncOptions>(config.GetSection(StudentSyncOptions.SectionName));
        services.AddSingleton<IExternalStudentSource, InMemoryExternalStudentSource>();
        services.AddSingleton<InMemoryExternalStudentSink>();
        services.AddSingleton<IExternalStudentSink>(sp => sp.GetRequiredService<InMemoryExternalStudentSink>());
        services.AddTransient<StudentExtractor>();
        services.AddTransient<StudentMapper>();
        services.AddTransient<StudentValidator>();
        services.AddTransient<StudentWriter>();
        services.AddTransient<StudentOutboxExtractor>();
        services.AddTransient<StudentOutboxMapper>();
        services.AddTransient<StudentOutboxValidator>();
        services.AddTransient<StudentOutboxWriter>();
        services.AddSingleton<ISyncModule, StudentSyncModule>();

        services.AddSingleton(new Mock<ISyncLogger>().Object);
        services.AddSingleton(new Mock<IBackgroundJobClient>().Object);
        services.AddSingleton(new Mock<ILogger<SyncRunRepository>>().Object);
        services.AddSingleton(new Mock<ILogger<SyncDispatcher>>().Object);
        services.AddSingleton(new Mock<ILogger<SyncModuleExecutor>>().Object);
        services.AddLogging();

        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<ISyncCheckpointStore, SyncCheckpointStore>();
        services.AddScoped<IFailureRepository>(_ => new Mock<IFailureRepository>().Object);

        services.AddSingleton<ISyncPipeline, SyncPipeline>();
        services.Configure<SyncOptions>(o => { o.Pipeline.PerBatchWriterRetryAttempts = 0; });

        // Ambient run-context accessor the executor pins per run (AsyncLocal).
        services.AddSingleton<SyncRunContextAccessor>();
        services.AddSingleton<ISyncRunContextAccessor>(sp => sp.GetRequiredService<SyncRunContextAccessor>());

        services.AddScoped<SyncModuleExecutor>();
        services.AddSingleton<ISyncModuleRegistry>(sp =>
        {
            var registryMock = new Mock<ISyncModuleRegistry>();
            foreach (var m in sp.GetServices<ISyncModule>())
            {
                registryMock.Setup(r => r.Resolve(m.ModuleName)).Returns(m);
            }
            return registryMock.Object;
        });

        _serviceProvider = services.BuildServiceProvider();
        _syncDb = _serviceProvider.GetRequiredService<SyncDbContext>();
    }

    [Fact]
    public async Task StudentPullSync_EndToEnd_ProcessesExternalStudents()
    {
        var executor = _serviceProvider.GetRequiredService<SyncModuleExecutor>();
        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "Test"
        };
        var cancellation = new Mock<IJobCancellationToken>();

        await executor.ExecuteAsync("students", SyncDirection.Pull, metadata, null, cancellation.Object);

        // Audit row records the validator outcome.
        var run = await _syncDb.Runs.FirstAsync(r => r.CorrelationId == metadata.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.RecordsProcessed.Should().Be(48, "50 emitted minus 2 dropped by StudentValidator (empty email)");
        run.RecordsFailed.Should().Be(2);

        // Gateway saw the validator-passing remainder.
        _capturedStudents.Should().HaveCount(48, "the writer forwards only the records the validator accepted");
        _capturedStudents.Select(s => s.ExternallySourced.ExternalId).Should().OnlyHaveUniqueItems();
        _capturedStudents.Should().AllSatisfy(s =>
        {
            s.ExternallySourced.ExternalId.Should().NotBeNullOrWhiteSpace();
            s.Name.Should().StartWith("{\"ar\":\"", "mapper normalizes to bilingual JSON");
            s.Email.Should().NotBeNullOrWhiteSpace();
        });

        var checkpoint = await _syncDb.Checkpoints.FirstOrDefaultAsync(c => c.ModuleName == "students");
        checkpoint.Should().NotBeNull();
        checkpoint!.Cursor.Should().NotBeNullOrEmpty();
    }
}
