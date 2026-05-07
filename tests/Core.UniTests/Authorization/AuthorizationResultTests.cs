using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using FluentAssertions;
using Xunit;
using System;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationResultTests
{
    [Fact]
    public void Deny_ShouldReturnResultWithIsAllowedFalse()
    {
        var result = AuthorizationResult.Deny();

        result.IsAllowed.Should().BeFalse();
        result.SourceType.Should().Be(SourceType.None);
        result.SourceId.Should().BeNull();
        result.AppliedDomain.Should().BeNull();
        result.AppliedYear.Should().BeNull();
        result.AppliedSemester.Should().BeNull();
    }

    [Fact]
    public void AllowFromOverride_ShouldReturnResultWithIsAllowedTrue()
    {
        var id = Guid.NewGuid();
        var result = AuthorizationResult.AllowFromOverride(id, "D1", "Y1", "S1");

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.UserOverride);
        result.SourceId.Should().Be(id);
        result.AppliedDomain.Should().Be("D1");
        result.AppliedYear.Should().Be("Y1");
        result.AppliedSemester.Should().Be("S1");
    }

    [Fact]
    public void AllowFromRole_ShouldReturnResultWithIsAllowedTrue()
    {
        var id = Guid.NewGuid();
        var result = AuthorizationResult.AllowFromRole(id, "D1", "Y1", "S1");

        result.IsAllowed.Should().BeTrue();
        result.SourceType.Should().Be(SourceType.RoleAssignment);
        result.SourceId.Should().Be(id);
        result.AppliedDomain.Should().Be("D1");
        result.AppliedYear.Should().Be("Y1");
        result.AppliedSemester.Should().Be("S1");
    }
}
