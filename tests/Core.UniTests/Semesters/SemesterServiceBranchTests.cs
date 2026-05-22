using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using DomainValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;
using NotFoundException = CapitalUniversity.Core.Domain.Common.Exceptions.NotFoundException;

namespace CapitalUniversity.Core.UniTests.Semesters;

public class SemesterServiceBranchTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISemesterRepository> _semesters = new();
    private readonly Mock<IAcademicYearRepository> _years = new();
    private readonly Mock<IValidator<CreateSemesterRequest>> _createValidator = new();
    private readonly Mock<IValidator<(Guid, UpdateSemesterRequest)>> _updateValidator = new();
    private readonly SemesterService _sut;

    public SemesterServiceBranchTests()
    {
        _uow.Setup(u => u.Semesters).Returns(_semesters.Object);
        _uow.Setup(u => u.AcademicYears).Returns(_years.Object);
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateSemesterRequest>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<(Guid, UpdateSemesterRequest)>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _sut = new SemesterService(_uow.Object, _createValidator.Object, _updateValidator.Object, new TestLocalizationService());
    }

    private static AcademicYear YearWithRange(DateTime start, DateTime end) =>
        new() { Name = "AY", StartDate = start, EndDate = end };

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        _semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Semester?)null);
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedResponse()
    {
        var s = new Semester { Name = "S1", AcademicYearId = Guid.NewGuid(), StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(60) };
        _semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(s);

        var result = await _sut.GetByIdAsync(s.Id);
        Assert.NotNull(result);
        Assert.Equal("S1", result!.Name);
    }

    [Fact]
    public async Task GetCurrentAsync_Missing_ReturnsNull()
    {
        _semesters.Setup(r => r.GetCurrentAsync()).ReturnsAsync((Semester?)null);
        Assert.Null(await _sut.GetCurrentAsync());
    }

    [Fact]
    public async Task GetCurrentAsync_Found_ReturnsMapped()
    {
        var s = new Semester { Name = "Curr" };
        _semesters.Setup(r => r.GetCurrentAsync()).ReturnsAsync(s);
        var r = await _sut.GetCurrentAsync();
        Assert.NotNull(r);
        Assert.Equal("Curr", r!.Name);
    }

    [Fact]
    public async Task GetByAcademicYearIdAsync_MapsAll()
    {
        _semesters.Setup(r => r.GetByAcademicYearIdAsync(It.IsAny<Guid>()))
                  .ReturnsAsync(new[] { new Semester { Name = "S1" }, new Semester { Name = "S2" } });

        var result = (await _sut.GetByAcademicYearIdAsync(Guid.NewGuid())).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CreateAsync_InvalidValidation_ThrowsValidationException()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateSemesterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Name", "required") }));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.CreateAsync(new CreateSemesterRequest { AcademicYearId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task CreateAsync_MissingYear_ThrowsValidationException()
    {
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.CreateAsync(new CreateSemesterRequest { AcademicYearId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task CreateAsync_DatesOutsideYear_Throws()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(year);

        await Assert.ThrowsAsync<DomainValidationException>(() => _sut.CreateAsync(new CreateSemesterRequest
        {
            AcademicYearId = year.Id,
            StartDate = new DateTime(2023, 12, 1),
            EndDate = new DateTime(2024, 6, 1)
        }));
    }

    [Fact]
    public async Task CreateAsync_Overlap_Throws()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(year);
        _semesters.Setup(r => r.HasOverlapAsync(year.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                  .ReturnsAsync(true);

        await Assert.ThrowsAsync<DomainValidationException>(() => _sut.CreateAsync(new CreateSemesterRequest
        {
            AcademicYearId = year.Id,
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2024, 6, 1)
        }));
    }

    [Fact]
    public async Task CreateAsync_Happy_PersistsAndReturnsId()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(year);
        _semesters.Setup(r => r.HasOverlapAsync(year.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                  .ReturnsAsync(false);
        _semesters.Setup(r => r.GetCurrentAsync()).ReturnsAsync((Semester?)null);
        Semester? added = null;
        _semesters.Setup(r => r.AddAsync(It.IsAny<Semester>()))
                  .Callback<Semester>(s => added = s).Returns(Task.CompletedTask);

        var id = await _sut.CreateAsync(new CreateSemesterRequest
        {
            AcademicYearId = year.Id,
            Name = "Fall",
            Order = 1,
            StartDate = new DateTime(2024, 2, 1),
            EndDate = new DateTime(2024, 6, 1)
        });

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(added);
    }

    [Fact]
    public async Task CreateAsync_CurrentSemester_DeactivatesOther()
    {
        var year = YearWithRange(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(1));
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(year);
        _semesters.Setup(r => r.HasOverlapAsync(year.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                  .ReturnsAsync(false);
        var prevCurrent = new Semester { IsCurrent = true };
        _semesters.Setup(r => r.GetCurrentAsync()).ReturnsAsync(prevCurrent);
        _semesters.Setup(r => r.AddAsync(It.IsAny<Semester>())).Returns(Task.CompletedTask);

        await _sut.CreateAsync(new CreateSemesterRequest
        {
            AcademicYearId = year.Id,
            StartDate = DateTime.UtcNow.AddDays(-5),
            EndDate = DateTime.UtcNow.AddDays(5)
        });

        Assert.False(prevCurrent.IsCurrent);
        _semesters.Verify(r => r.Update(prevCurrent), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_InvalidValidation_Throws()
    {
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<(Guid, UpdateSemesterRequest)>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Name", "bad") }));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateSemesterRequest()));
    }

    [Fact]
    public async Task UpdateAsync_SemesterMissing_ThrowsNotFound()
    {
        _semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Semester?)null);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateSemesterRequest()));
    }

    [Fact]
    public async Task UpdateAsync_YearMissing_ThrowsNotFound()
    {
        _semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                  .ReturnsAsync(new Semester { AcademicYearId = Guid.NewGuid() });
        _years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateAsync(Guid.NewGuid(), new UpdateSemesterRequest()));
    }

    [Fact]
    public async Task UpdateAsync_EndBeforeStart_Throws()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        var sem = new Semester { AcademicYearId = year.Id, StartDate = new DateTime(2024, 2, 1), EndDate = new DateTime(2024, 5, 1) };
        _semesters.Setup(r => r.GetByIdAsync(sem.Id)).ReturnsAsync(sem);
        _years.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.UpdateAsync(sem.Id, new UpdateSemesterRequest
            {
                StartDate = new DateTime(2024, 5, 1),
                EndDate = new DateTime(2024, 4, 1)
            }));
    }

    [Fact]
    public async Task UpdateAsync_OutsideYearRange_Throws()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        var sem = new Semester { AcademicYearId = year.Id, StartDate = new DateTime(2024, 2, 1), EndDate = new DateTime(2024, 5, 1) };
        _semesters.Setup(r => r.GetByIdAsync(sem.Id)).ReturnsAsync(sem);
        _years.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.UpdateAsync(sem.Id, new UpdateSemesterRequest
            {
                StartDate = new DateTime(2023, 12, 1),
                EndDate = new DateTime(2024, 6, 1)
            }));
    }

    [Fact]
    public async Task UpdateAsync_Overlap_Throws()
    {
        var year = YearWithRange(new DateTime(2024, 1, 1), new DateTime(2024, 12, 31));
        var sem = new Semester { AcademicYearId = year.Id, StartDate = new DateTime(2024, 2, 1), EndDate = new DateTime(2024, 5, 1) };
        _semesters.Setup(r => r.GetByIdAsync(sem.Id)).ReturnsAsync(sem);
        _years.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _semesters.Setup(r => r.HasOverlapAsync(year.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), sem.Id))
                  .ReturnsAsync(true);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _sut.UpdateAsync(sem.Id, new UpdateSemesterRequest { Name = "x" }));
    }

    [Fact]
    public async Task UpdateAsync_Happy_PersistsUpdate()
    {
        var year = YearWithRange(DateTime.UtcNow.AddYears(-1), DateTime.UtcNow.AddYears(1));
        var sem = new Semester { AcademicYearId = year.Id, StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(5) };
        _semesters.Setup(r => r.GetByIdAsync(sem.Id)).ReturnsAsync(sem);
        _years.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _semesters.Setup(r => r.HasOverlapAsync(year.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), sem.Id))
                  .ReturnsAsync(false);
        _semesters.Setup(r => r.GetCurrentAsync()).ReturnsAsync((Semester?)null);

        await _sut.UpdateAsync(sem.Id, new UpdateSemesterRequest { Name = "Spring" });

        _semesters.Verify(r => r.Update(sem), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Missing_Throws()
    {
        _semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Semester?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesAndSaves()
    {
        var sem = new Semester();
        _semesters.Setup(r => r.GetByIdAsync(sem.Id)).ReturnsAsync(sem);

        await _sut.DeleteAsync(sem.Id);

        _semesters.Verify(r => r.Delete(sem), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
