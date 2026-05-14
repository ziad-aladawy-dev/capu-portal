
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationScopeTests
{
    [Fact]
    public void AuthorizationScope_ShouldInitializeProperties()
    {
        var nodeId = System.Guid.NewGuid();
        var nodePath = "/1/2";

        var scope = new AuthorizationScope
        {
            Domain = "F1",
            Year = "Y1",
            Semester = "S1",
            StructureNodeId = nodeId,
            StructureNodePath = nodePath
        };

        scope.Domain.Should().Be("F1");
        scope.Year.Should().Be("Y1");
        scope.Semester.Should().Be("S1");
        scope.StructureNodeId.Should().Be(nodeId);
        scope.StructureNodePath.Should().Be(nodePath);
    }
}
