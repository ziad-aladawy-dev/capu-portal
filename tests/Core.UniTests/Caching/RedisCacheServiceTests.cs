using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.CrossCutting.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Caching;

public class RedisCacheServiceTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly RedisCacheService _sut;

    public RedisCacheServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _sut = new RedisCacheService(_mockCache.Object);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ReturnsDeserializedObject()
    {
        // Arrange
        var key = "test_key";
        var expectedObj = new TestObject { Id = 1, Name = "Test" };
        var json = JsonSerializer.Serialize(expectedObj);
        var bytes = Encoding.UTF8.GetBytes(json);

        _mockCache.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _sut.GetAsync<TestObject>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(expectedObj.Id);
        result.Name.Should().Be(expectedObj.Name);
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsDefault()
    {
        // Arrange
        var key = "missing_key";

        _mockCache.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!);

        // Act
        var result = await _sut.GetAsync<TestObject>(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_SerializesAndSetsInCache()
    {
        // Arrange
        var key = "test_key";
        var obj = new TestObject { Id = 1, Name = "Test" };
        var expiration = TimeSpan.FromMinutes(10);

        // Act
        await _sut.SetAsync(key, obj, expiration);

        var serializedObj = JsonSerializer.Serialize(obj);

        // Assert
        _mockCache.Verify(c => c.SetAsync(
            key,
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == serializedObj),
            It.Is<DistributedCacheEntryOptions>(opts => opts.AbsoluteExpirationRelativeToNow == expiration),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_CallsRemoveOnCache()
    {
        // Arrange
        var key = "test_key";

        // Act
        await _sut.RemoveAsync(key);

        // Assert
        _mockCache.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
