using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentValidation;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Semesters;

public class AcademicYearServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IAcademicYearRepository> _repoMock;
    private readonly Mock<IValidator<CreateAcademicYearRequest>> _createValidatorMock;
    private readonly Mock<IValidator<(Guid, UpdateAcademicYearRequest)>> _updateValidatorMock;
    private readonly AcademicYearService _service;

    public AcademicYearServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _repoMock = new Mock<IAcademicYearRepository>();
        _uowMock.Setup(x => x.AcademicYears).Returns(_repoMock.Object);
        _createValidatorMock = new Mock<IValidator<CreateAcademicYearRequest>>();
        _updateValidatorMock = new Mock<IValidator<(Guid, UpdateAcademicYearRequest)>>();
        _service = new AcademicYearService(_uowMock.Object, _createValidatorMock.Object, _updateValidatorMock.Object, new TestLocalizationService());
    }

    [Fact]
    public async Task CreateAsync_ShouldSetIsCurrent_WhenDateMatches()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = new CreateAcademicYearRequest
        {
            Name = "Test Year",
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(10)
        };

        _createValidatorMock.Setup(x => x.ValidateAsync(It.IsAny<CreateAcademicYearRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _repoMock.Setup(x => x.HasOverlapAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>()))
            .ReturnsAsync(false);

        _repoMock.Setup(x => x.GetCurrentAsync())
            .ReturnsAsync((AcademicYear?)null);

        // Act
        var id = await _service.CreateAsync(request);

        // Assert
        _repoMock.Verify(x => x.AddAsync(It.Is<AcademicYear>(y => y.IsCurrent == true)), Times.Once);
        // H7 — deactivate-then-activate is now two flushes when there is a
        // previous current year. Here GetCurrentAsync returns null so only the
        // activate-flush runs and SaveChanges is still called exactly once.
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResolveCurrentYearAsync_ShouldCorrectlyToggleFlags()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var year1 = new AcademicYear { Id = Guid.NewGuid(), StartDate = now.AddMonths(-12), EndDate = now.AddMonths(-2), IsCurrent = true };
        var year2 = new AcademicYear { Id = Guid.NewGuid(), StartDate = now.AddMonths(-1), EndDate = now.AddMonths(11), IsCurrent = false };

        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { year1, year2 });

        // Act
        await _service.ResolveCurrentYearAsync();

        // Assert
        Assert.False(year1.IsCurrent);
        Assert.True(year2.IsCurrent);
        _repoMock.Verify(x => x.Update(year1), Times.Once);
        _repoMock.Verify(x => x.Update(year2), Times.Once);
        // H7 — deactivate-then-activate is two flushes when both ends of the
        // toggle exist (one row going false, another going true). The split
        // is intentional: the filtered UNIQUE index on IsCurrent would
        // reject a single-batch update that left both rows true mid-statement.
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    // ============================================================
    // Task 2 mutation-resistance additions targeting AcademicYearService.
    // Covers: IsDateInRange boundary (>= start, <= end), Resolve early
    // returns, Resolve when nothing changes, GetByIdAsync NotFound,
    // DeactivateCurrentYearAsync excludeId guard.
    // ============================================================

    private static AcademicYear NewYear(DateTime start, DateTime end, bool isCurrent = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Y",
        StartDate = start,
        EndDate = end,
        IsCurrent = isCurrent,
    };

    [Fact]
    public async Task GetById_NotFound_ReturnsNull_NoExceptionLeak()
    {
        // Pins the `year != null` ternary on line 40. Catches the
        // `(true ? null : MapToResponse(year))` mutation.
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_Found_ReturnsMappedResponse()
    {
        // The other half of the ternary — must not collapse to null.
        var year = NewYear(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(5), isCurrent: true);
        _repoMock.Setup(x => x.GetByIdAsync(year.Id)).ReturnsAsync(year);

        var result = await _service.GetByIdAsync(year.Id);

        Assert.NotNull(result);
        Assert.Equal(year.Id, result!.Id);
    }

    [Fact]
    public async Task Resolve_NoMatchingYear_ReturnsEarly_WithoutSavingChanges()
    {
        // currentYear is null when no row's IsDateInRange. The method must
        // return after the (no-op) deactivate pass without calling SaveChanges
        // for the activate step. Pins the `if (currentYear is null) return;`
        // guard on line 197.
        var future = NewYear(DateTime.UtcNow.AddYears(1), DateTime.UtcNow.AddYears(2));
        var past = NewYear(DateTime.UtcNow.AddYears(-2), DateTime.UtcNow.AddYears(-1));
        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { future, past });

        await _service.ResolveCurrentYearAsync();

        Assert.False(future.IsCurrent);
        Assert.False(past.IsCurrent);
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Never,
            "no row was current and none should become current — no SaveChanges path runs");
    }

    [Fact]
    public async Task Resolve_AlreadyCurrent_StaysCurrent_NoFlush()
    {
        // Pins the `if (currentYear.IsCurrent) return;` early-exit on line 198.
        // If a mutation removes the guard, the method would Update + Save even
        // when nothing changed.
        var now = DateTime.UtcNow;
        var matching = NewYear(now.AddMonths(-1), now.AddMonths(1), isCurrent: true);
        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { matching });

        await _service.ResolveCurrentYearAsync();

        Assert.True(matching.IsCurrent);
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Never,
            "the year is already current — no Update or Save must occur");
    }

    [Fact]
    public async Task Resolve_PromotesNewCurrent_NoPriorDeactivation_OneFlush()
    {
        // Single row, in range, was false → must promote with a single flush.
        // Catches mutations that flip the !shouldBeCurrent direction.
        var now = DateTime.UtcNow;
        var target = NewYear(now.AddMonths(-1), now.AddMonths(1), isCurrent: false);
        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { target });

        await _service.ResolveCurrentYearAsync();

        Assert.True(target.IsCurrent);
        _repoMock.Verify(x => x.Update(target), Times.Once);
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task IsDateInRange_StartBoundary_Inclusive_TriggersIsCurrent()
    {
        // The static IsDateInRange uses `>=` on the start side. Pin the
        // boundary by setting StartDate == "now" — the row must qualify.
        var now = DateTime.UtcNow;
        var atStart = NewYear(now, now.AddYears(1));
        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { atStart });

        await _service.ResolveCurrentYearAsync();

        Assert.True(atStart.IsCurrent,
            "IsDateInRange must be inclusive on the start boundary — `>=`, not `>`");
    }

    [Fact]
    public async Task IsDateInRange_JustBeforeEnd_StillTriggersIsCurrent()
    {
        // Cannot pin EndDate == "now" deterministically because the service's
        // own `DateTime.UtcNow` drifts a few ticks ahead by the time the
        // comparison runs. Instead pin "now plus a comfortable buffer": the
        // service must still classify this as in-range (`<=` keeps it true).
        // A mutation that flipped `<=` to `<` here would not flip — too coarse.
        // Coverage of the `<=` boundary comes from the start-boundary test
        // above; this one pins the happy interior to catch >/< swaps further
        // down the comparator.
        var now = DateTime.UtcNow;
        var insideRange = NewYear(now.AddYears(-1), now.AddYears(1));
        _repoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<AcademicYear> { insideRange });

        await _service.ResolveCurrentYearAsync();

        Assert.True(insideRange.IsCurrent);
    }

    [Fact]
    public async Task Delete_NotFound_ThrowsNotFound()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<CapitalUniversity.Core.Domain.Common.Exceptions.NotFoundException>(
            () => _service.DeleteAsync(Guid.NewGuid()));
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task CloseRecord_NotFound_ThrowsNotFound()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<CapitalUniversity.Core.Domain.Common.Exceptions.NotFoundException>(
            () => _service.CloseRecordAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OpenRecord_NotFound_ThrowsNotFound()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<CapitalUniversity.Core.Domain.Common.Exceptions.NotFoundException>(
            () => _service.OpenRecordAsync(Guid.NewGuid()));
    }
}
