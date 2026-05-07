using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationScopeTests
{
    [Fact]
    public void AuthorizationScope_ShouldInitializeProperties()
    {
        var scope = new AuthorizationScope
        {
            Domain = "F1",
            Year = "Y1",
            Semester = "S1"
        };

        scope.Domain.Should().Be("F1");
        scope.Year.Should().Be("Y1");
        scope.Semester.Should().Be("S1");
    }
}
