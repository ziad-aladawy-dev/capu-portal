using System.Linq;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using CapitalUniversity.Sync.Infrastructure.Pipeline;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

public class SyncPipelineTests
{
    private readonly Mock<ISyncLogger> _loggerMock = new();
    private readonly Mock<IOptionsMonitor<SyncOptions>> _optionsMock = new();
    private readonly SyncPipeline _sut;

    public SyncPipelineTests()
    {
        _optionsMock.Setup(x => x.CurrentValue).Returns(new SyncOptions());
        _sut = new SyncPipeline(
            _loggerMock.Object,
            _optionsMock.Object,
            alertingHook: null);
    }

    [Fact]
    public async Task RunAsync_EmptySource_ReturnsSuccessWithZeroProcessed()
    {
        // Arrange
        var metadata = new SyncRunMetadata { CorrelationId = Guid.NewGuid(), TriggeredBy = "Test" };
        var context = new SyncContext 
        { 
            ModuleName = "test", 
            Direction = SyncDirection.Pull,
            Metadata = metadata
        };
        var extractorMock = new Mock<IDataExtractor<string>>();
        extractorMock.Setup(x => x.ExtractAsync(context, It.IsAny<SyncCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<string>());

        var request = new SyncPipelineRequest<string, string>
        {
            Context = context,
            Extractor = extractorMock.Object,
            Mapper = new IdentityMapper(),
            Writer = new Mock<IRecordWriter<string>>().Object,
            ExternalKeySelector = x => x,
            BatchSize = 100
        };

        // Act
        var result = await _sut.RunAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsProcessed.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithRecords_ProcessesBatches()
    {
        // Arrange
        var metadata = new SyncRunMetadata { CorrelationId = Guid.NewGuid(), TriggeredBy = "Test" };
        var context = new SyncContext 
        { 
            ModuleName = "test", 
            Direction = SyncDirection.Pull,
            Metadata = metadata
        };
        var records = new[] { "1", "2", "3" };
        var extractorMock = new Mock<IDataExtractor<string>>();
        extractorMock.Setup(x => x.ExtractAsync(context, It.IsAny<SyncCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(records.ToAsyncEnumerable());

        var writerMock = new Mock<IRecordWriter<string>>();
        writerMock.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> batch, CancellationToken _) => batch.Count);

        var request = new SyncPipelineRequest<string, string>
        {
            Context = context,
            Extractor = extractorMock.Object,
            Mapper = new IdentityMapper(),
            Writer = writerMock.Object,
            ExternalKeySelector = x => x,
            BatchSize = 2
        };

        // Act
        var result = await _sut.RunAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsProcessed.Should().Be(3);
        writerMock.Verify(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_IdempotencyDedup_SkipsDuplicateKeysInRun()
    {
        // Arrange
        var metadata = new SyncRunMetadata { CorrelationId = Guid.NewGuid(), TriggeredBy = "Test" };
        var context = new SyncContext 
        { 
            ModuleName = "test", 
            Direction = SyncDirection.Pull,
            Metadata = metadata
        };
        var records = new[] { "key1", "key1", "key2" };
        var extractorMock = new Mock<IDataExtractor<string>>();
        extractorMock.Setup(x => x.ExtractAsync(context, It.IsAny<SyncCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(records.ToAsyncEnumerable());

        var writerMock = new Mock<IRecordWriter<string>>();
        writerMock.Setup(x => x.UpsertBatchAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> batch, CancellationToken _) => batch.Count);

        var request = new SyncPipelineRequest<string, string>
        {
            Context = context,
            Extractor = extractorMock.Object,
            Mapper = new IdentityMapper(),
            Writer = writerMock.Object,
            ExternalKeySelector = x => x,
            BatchSize = 10
        };

        // Act
        var result = await _sut.RunAsync(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordsProcessed.Should().Be(2); // key1 (once), key2
        _loggerMock.Verify(x => x.LogDebug(context.CorrelationId, It.Is<string>(s => s.Contains("Idempotency skip")), It.IsAny<object[]>()), Times.Once);
    }

    private class IdentityMapper : IRecordMapper<string, string>
    {
        public string Map(string external) => external;
    }
}