using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class ScopeKeysTests
{
    [Fact]
    public void Global_IsStableSentinel()
    {
        Assert.Equal("Global", ScopeKeys.Global);
    }

    [Fact]
    public void FromGuid_NullId_ReturnsGlobalSentinel()
    {
        Assert.Equal(ScopeKeys.Global, ScopeKeys.FromGuid(null));
    }

    [Fact]
    public void FromGuid_ConcreteId_ReturnsGuidString()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id.ToString(), ScopeKeys.FromGuid(id));
    }

    [Fact]
    public void IsGlobal_ExactSentinel_True()
    {
        Assert.True(ScopeKeys.IsGlobal(ScopeKeys.Global));
    }

    [Theory]
    [InlineData("global")] // case-sensitive
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Other")]
    public void IsGlobal_NonSentinel_False(string? value)
    {
        Assert.False(ScopeKeys.IsGlobal(value));
    }
}