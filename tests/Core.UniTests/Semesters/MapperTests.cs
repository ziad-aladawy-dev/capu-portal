using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters.Mappings;
using CapitalUniversity.Core.Domain.Semsters;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Semesters;

public class MapperTests
{
    private readonly AcademicYearMapper _yearMapper = new();
    private readonly SemesterMapper _semesterMapper = new();

    [Fact]
    public void AcademicYear_UpdateEntity_ShouldIgnoreNulls()
    {
        // Arrange
        var entity = new AcademicYear
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30)
        };

        var request = new UpdateAcademicYearRequest
        {
            Name = "New Name",
            StartDate = null,
            EndDate = null
        };

        // Act
        _yearMapper.UpdateEntity(request, entity);

        // Assert
        Assert.Equal("New Name", entity.Name);
        Assert.Equal(new DateTime(2025, 9, 1), entity.StartDate);
        Assert.Equal(new DateTime(2026, 6, 30), entity.EndDate);
    }

    [Fact]
    public void Semester_UpdateEntity_ShouldIgnoreNulls()
    {
        // Arrange
        var entity = new Semester
        {
            Id = Guid.NewGuid(),
            Name = "Fall",
            Order = 1,
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2025, 12, 31)
        };

        var request = new UpdateSemesterRequest
        {
            Name = null,
            Order = 2,
            StartDate = null,
            EndDate = null
        };

        // Act
        _semesterMapper.UpdateEntity(request, entity);

        // Assert
        Assert.Equal("Fall", entity.Name);
        Assert.Equal(2, entity.Order);
        Assert.Equal(new DateTime(2025, 9, 1), entity.StartDate);
        Assert.Equal(new DateTime(2025, 12, 31), entity.EndDate);
    }
}
