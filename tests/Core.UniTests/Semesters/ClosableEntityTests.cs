using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Semsters;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Semesters;

/// <summary>
/// Plan-required tests for the closable entity model. Cf.
/// Master_Refactor_Plan.md § "Closable entities".
/// </summary>
public class ClosableEntityTests
{
    [Fact]
    public void AcademicYear_EnsureMutable_ThrowsWhenClosed()
    {
        var year = new AcademicYear { Name = "2025/2026", IsClosed = true };
        var act = () => year.EnsureMutable();
        act.Should().Throw<ConflictException>().WithMessage("*closed*");
    }

    [Fact]
    public void AcademicYear_Close_FlipsFlagAndStampsClosedAt()
    {
        var year = new AcademicYear { Name = "2025/2026" };

        year.Close();

        year.IsClosed.Should().BeTrue();
        year.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void AcademicYear_Close_RejectsDoubleClose()
    {
        var year = new AcademicYear { Name = "2025/2026" };
        year.Close();

        var act = () => year.Close();

        act.Should().Throw<ConflictException>().WithMessage("*already closed*");
    }

    [Fact]
    public void AcademicYear_Reopen_RequiresClosedState()
    {
        var year = new AcademicYear { Name = "2025/2026" };

        var act = () => year.Reopen();

        act.Should().Throw<ConflictException>().WithMessage("*not closed*");
    }

    [Fact]
    public void AcademicYear_Reopen_ClearsClosedState()
    {
        var year = new AcademicYear { Name = "2025/2026" };
        year.Close();

        year.Reopen();

        year.IsClosed.Should().BeFalse();
        year.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Semester_EnsureMutable_ThrowsWhenClosed()
    {
        var sem = new Semester { Name = "Fall", IsClosed = true };
        var act = () => sem.EnsureMutable();
        act.Should().Throw<ConflictException>().WithMessage("*closed*");
    }

    [Fact]
    public void Semester_Close_FlipsFlagAndStampsClosedAt()
    {
        var sem = new Semester { Name = "Fall" };

        sem.Close();

        sem.IsClosed.Should().BeTrue();
        sem.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void Semester_Reopen_DoesNotImplyClose()
    {
        // EditClose closes; reopening requires a separate Open permission AND
        // is a distinct domain action — Reopen alone doesn't toggle to closed.
        var sem = new Semester { Name = "Fall" };
        sem.Close();
        sem.Reopen();

        sem.IsClosed.Should().BeFalse();
    }
}
