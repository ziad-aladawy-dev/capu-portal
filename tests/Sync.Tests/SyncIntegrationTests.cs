using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Dispatching;
using CapitalUniversity.Sync.Infrastructure.Execution;
using CapitalUniversity.Sync.Infrastructure.Pipeline;
using CapitalUniversity.Sync.Infrastructure.Observability;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

public class SyncIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SyncDbContext _syncDb;
    private readonly StudentSyncDbContext _studentDb;
    private readonly string _syncDbName = "SyncDb_" + Guid.NewGuid();
    private readonly string _studentDbName = "StudentDb_" + Guid.NewGuid();

    public SyncIntegrationTests()
    {
        var services = new ServiceCollection();

        // Configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sync:Student:ConnectionString"] = "Server=none",
                ["Sync:Student:BatchSize"] = "10"
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        // Databases - Use constant names per test run
        services.AddDbContext<SyncDbContext>(options => 
            options.UseInMemoryDatabase(_syncDbName));
        services.AddDbContext<StudentSyncDbContext>(options => 
            options.UseInMemoryDatabase(_studentDbName));

        // Student Module Parts
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

        // Core Infrastructure Mocks
        services.AddSingleton(new Mock<ISyncLogger>().Object);
        services.AddSingleton(new Mock<IBackgroundJobClient>().Object);
        services.AddSingleton(new Mock<ILogger<SyncRunRepository>>().Object);
        services.AddSingleton(new Mock<ILogger<SyncDispatcher>>().Object);
        services.AddSingleton(new Mock<ILogger<SyncModuleExecutor>>().Object);
        services.AddLogging();

        // Repositories
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<ISyncCheckpointStore, SyncCheckpointStore>();
        services.AddScoped<IFailureRepository>(sp => new Mock<IFailureRepository>().Object);

        // Pipeline is a stateless singleton; extractor/mapper/writer come from
        // the per-call SyncPipelineRequest.
        services.AddSingleton<ISyncPipeline, SyncPipeline>();
        services.Configure<SyncOptions>(options => {
            options.Pipeline.PerBatchWriterRetryAttempts = 0;
        });

        // Executor & Registry
        services.AddScoped<SyncModuleExecutor>();
        services.AddSingleton<ISyncModuleRegistry>(sp => {
            var registryMock = new Mock<ISyncModuleRegistry>();
            var modules = sp.GetServices<ISyncModule>();
            foreach (var m in modules)
            {
                registryMock.Setup(r => r.Resolve(m.ModuleName)).Returns(m);
            }
            return registryMock.Object;
        });

        _serviceProvider = services.BuildServiceProvider();
        _syncDb = _serviceProvider.GetRequiredService<SyncDbContext>();
        _studentDb = _serviceProvider.GetRequiredService<StudentSyncDbContext>();
    }

    [Fact]
    public async Task StudentPullSync_EndToEnd_ProcessesExternalStudents()
    {
        // Arrange
        var executor = _serviceProvider.GetRequiredService<SyncModuleExecutor>();
        var metadata = new SyncRunMetadata
        {
            CorrelationId = Guid.NewGuid(),
            TriggeredBy = "Test"
        };
        var mockCancellationToken = new Mock<IJobCancellationToken>();

        // Act
        await executor.ExecuteAsync("students", SyncDirection.Pull, metadata, null, mockCancellationToken.Object);

        // Assert
        var run = await _syncDb.Runs.FirstAsync(r => r.CorrelationId == metadata.CorrelationId);
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.RecordsProcessed.Should().Be(48);
        // Phase X.3 fix #6: SyncPipeline now constructs SyncResult directly with
        // RecordsFailed honestly counted (validator drops + writer-skipped). The
        // 2-of-50 records dropped by StudentValidator (empty email) should appear
        // here — confirming the audit is no longer "fraudulent".
        run.RecordsFailed.Should().Be(2);

        // Verify data in student DB
        _studentDb.Students.Count().Should().Be(48);

        var checkpoint = await _syncDb.Checkpoints.FirstOrDefaultAsync(c => c.ModuleName == "students");
        checkpoint.Should().NotBeNull();
        checkpoint!.Cursor.Should().NotBeNullOrEmpty();
    }
}