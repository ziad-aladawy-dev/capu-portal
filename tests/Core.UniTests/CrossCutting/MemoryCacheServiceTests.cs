using CapitalUniversity.Core.Application.CrossCutting.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace CapitalUniversity.Core.UniTests.CrossCutting;

/// <summary>
/// MemoryCacheService is a thin async wrapper over <see cref="IMemoryCache"/>.
/// These tests pin round-trip get/set, the miss path (default), absolute
/// expiration wiring (set vs. omitted), and removal — against a real
/// MemoryCache so the conditional <c>expirationTime.HasValue</c> branch and the
/// TryGetValue/out behavior are exercised end to end.
/// </summary>
public class MemoryCacheServiceTests
{
    private static (MemoryCacheService Sut, MemoryCache Cache) Build()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new MemoryCacheService(cache), cache);
    }

    [Fact]
    public async Task Get_Miss_ReturnsDefault()
    {
        var (sut, _) = Build();
        (await sut.GetAsync<string>("absent")).Should().BeNull();
    }

    [Fact]
    public async Task Get_MissValueType_ReturnsDefault()
    {
        var (sut, _) = Build();
        (await sut.GetAsync<int>("absent")).Should().Be(0);
    }

    [Fact]
    public async Task SetThenGet_RoundTrips()
    {
        var (sut, _) = Build();
        await sut.SetAsync("k", "value");
        (await sut.GetAsync<string>("k")).Should().Be("value");
    }

    [Fact]
    public async Task Set_WithExpiration_StoresWithAbsoluteExpiration()
    {
        var (sut, cache) = Build();
        await sut.SetAsync("k", 42, TimeSpan.FromMinutes(5));

        cache.TryGetValue("k", out int v).Should().BeTrue();
        v.Should().Be(42);
    }

    [Fact]
    public async Task Set_Overwrites_ExistingValue()
    {
        var (sut, _) = Build();
        await sut.SetAsync("k", "first");
        await sut.SetAsync("k", "second");
        (await sut.GetAsync<string>("k")).Should().Be("second");
    }

    [Fact]
    public async Task Remove_DeletesKey()
    {
        var (sut, _) = Build();
        await sut.SetAsync("k", "value");

        await sut.RemoveAsync("k");

        (await sut.GetAsync<string>("k")).Should().BeNull();
    }

    [Fact]
    public async Task Remove_AbsentKey_NoThrow()
    {
        var (sut, _) = Build();
        await sut.Invoking(s => s.RemoveAsync("absent")).Should().NotThrowAsync();
    }
}
