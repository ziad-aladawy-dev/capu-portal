using System.Text;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Application.CrossCutting.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Caching;

/// <summary>
/// Distributed cache failures must degrade to "no cache", never bubble up. Logger
/// (when supplied) must be invoked exactly once per failed op.
/// </summary>
public class RedisCacheServiceFailureTests
{
    private sealed class Dto
    {
        public int Id { get; set; }
    }

    [Fact]
    public async Task GetAsync_BackendThrows_SwallowsAndReturnsDefault()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("redis down"));
        var logger = new Mock<IAppLogger>();
        var sut = new RedisCacheService(cache.Object, logger.Object);

        var result = await sut.GetAsync<Dto>("k");

        Assert.Null(result);
        logger.Verify(l => l.LogWarningAsync(It.IsAny<string>(), "RedisCacheService", null, It.IsAny<Dictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_MalformedJson_SwallowsAndReturnsDefault()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Encoding.UTF8.GetBytes("not-valid-json"));
        var sut = new RedisCacheService(cache.Object);

        var result = await sut.GetAsync<Dto>("k");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_BackendThrows_DoesNotPropagate()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                                    It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("write failed"));
        var sut = new RedisCacheService(cache.Object);

        var ex = await Record.ExceptionAsync(() => sut.SetAsync("k", new Dto { Id = 1 }));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SetAsync_NoExpiration_StillPersists()
    {
        var cache = new Mock<IDistributedCache>();
        var sut = new RedisCacheService(cache.Object);

        await sut.SetAsync("k", new Dto { Id = 7 });

        cache.Verify(c => c.SetAsync("k", It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_BackendThrows_DoesNotPropagate()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("remove failed"));
        var sut = new RedisCacheService(cache.Object);

        var ex = await Record.ExceptionAsync(() => sut.RemoveAsync("k"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task GetAsync_NullLogger_FailureStillSwallowed()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException());
        var sut = new RedisCacheService(cache.Object, logger: null);

        var result = await sut.GetAsync<Dto>("k");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_LoggerItselfThrows_DoesNotPropagate()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                                    It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("write failed"));
        var logger = new Mock<IAppLogger>();
        logger.Setup(l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(),
                                            It.IsAny<Microsoft.AspNetCore.Http.HttpContext>(),
                                            It.IsAny<Dictionary<string, object>>()))
              .ThrowsAsync(new InvalidOperationException("logger down"));
        var sut = new RedisCacheService(cache.Object, logger.Object);

        var ex = await Record.ExceptionAsync(() => sut.SetAsync("k", new Dto()));

        Assert.Null(ex);
    }
}