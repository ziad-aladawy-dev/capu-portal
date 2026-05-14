using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters;
using CapitalUniversity.Core.Domain.Semsters;
using FluentValidation;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Semesters;

public class SemesterServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ISemesterRepository> _semesterRepoMock;
    private readonly Mock<IAcademicYearRepository> _yearRepoMock;
    private readonly Mock<IValidator<CreateSemesterRequest>> _createValidatorMock;
    private readonly Mock<IValidator<(Guid, UpdateSemesterRequest)>> _updateValidatorMock;
    private readonly SemesterService _service;

    public SemesterServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _semesterRepoMock = new Mock<ISemesterRepository>();
        _yearRepoMock = new Mock<IAcademicYearRepository>();
        _uowMock.Setup(x => x.Semesters).Returns(_semesterRepoMock.Object);
        _uowMock.Setup(x => x.AcademicYears).Returns(_yearRepoMock.Object);
        _createValidatorMock = new Mock<IValidator<CreateSemesterRequest>>();
        _updateValidatorMock = new Mock<IValidator<(Guid, UpdateSemesterRequest)>>();
        _service = new SemesterService(_uowMock.Object, _createValidatorMock.Object, _updateValidatorMock.Object);
    }

    [Fact]
    public async Task ResolveCurrentSemesterAsync_ShouldDeactivateAll_WhenNoCurrentYear()
    {
        // Arrange
        _yearRepoMock.Setup(x => x.GetCurrentAsync()).ReturnsAsync((AcademicYear?)null);
        var currentSemester = new Semester { IsCurrent = true };
        _semesterRepoMock.Setup(x => x.GetCurrentAsync()).ReturnsAsync(currentSemester);

        // Act
        await _service.ResolveCurrentSemesterAsync();

        // Assert
        Assert.False(currentSemester.IsCurrent);
        _semesterRepoMock.Verify(x => x.Update(currentSemester), Times.Once);
    }

    [Fact]
    public async Task ResolveCurrentSemesterAsync_ShouldToggleFlags_WithinCurrentYear()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var year = new AcademicYear { Id = Guid.NewGuid(), IsCurrent = true };
        var sem1 = new Semester { Id = Guid.NewGuid(), AcademicYearId = year.Id, StartDate = now.AddMonths(-4), EndDate = now.AddMonths(-1), IsCurrent = true };
        var sem2 = new Semester { Id = Guid.NewGuid(), AcademicYearId = year.Id, StartDate = now.AddDays(-1), EndDate = now.AddMonths(3), IsCurrent = false };

        _yearRepoMock.Setup(x => x.GetCurrentAsync()).ReturnsAsync(year);
        _semesterRepoMock.Setup(x => x.GetByAcademicYearIdAsync(year.Id)).ReturnsAsync(new List<Semester> { sem1, sem2 });

        // Act
        await _service.ResolveCurrentSemesterAsync();

        // Assert
        Assert.False(sem1.IsCurrent);
        Assert.True(sem2.IsCurrent);
        _semesterRepoMock.Verify(x => x.Update(sem1), Times.Once);
        _semesterRepoMock.Verify(x => x.Update(sem2), Times.Once);
    }
}
