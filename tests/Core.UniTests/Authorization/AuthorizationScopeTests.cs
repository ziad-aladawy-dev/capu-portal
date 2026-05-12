using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authorization;

public class AuthorizationScopeTests
{
    [Fact]
    public void AuthorizationScope_ShouldInitializeProperties()
    {
        var universityId = System.Guid.NewGuid();
        var facultyId = System.Guid.NewGuid();
        var programId = System.Guid.NewGuid();

        var scope = new AuthorizationScope
        {
            Domain = "F1",
            Year = "Y1",
            Semester = "S1",
            UniversityId = universityId,
            FacultyId = facultyId,
            ProgramId = programId
        };

        scope.Domain.Should().Be("F1");
        scope.Year.Should().Be("Y1");
        scope.Semester.Should().Be("S1");
        scope.UniversityId.Should().Be(universityId);
        scope.FacultyId.Should().Be(facultyId);
        scope.ProgramId.Should().Be(programId);
    }
}
