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
        _uowMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }
}
