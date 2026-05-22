using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters.Mappings;
using CapitalUniversity.Core.Domain.Semsters;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Semesters;

public class MapperTests
{
    [Fact]
    public void AcademicYear_UpdateEntity_ShouldIgnoreNulls()
    {
        var mapper = new AcademicYearMapper();
        var year = new AcademicYear
        {
            Name = "Old Name",
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 12, 31)
        };

        var request = new UpdateAcademicYearRequest
        {
            Name = "New Name",
            StartDate = null // Should be ignored
        };

        mapper.UpdateEntity(request, year);

        // Name is normalized to JSON
        Assert.Equal("{\"ar\":\"New Name\",\"en\":\"New Name\"}", year.Name);
        Assert.Equal(new DateTime(2023, 1, 1), year.StartDate);
    }

    [Fact]
    public void Semester_UpdateEntity_ShouldIgnoreNulls()
    {
        var mapper = new SemesterMapper();
        var semester = new Semester
        {
            Name = "Fall",
            StartDate = new DateTime(2023, 9, 1),
            EndDate = new DateTime(2024, 1, 31)
        };

        var request = new UpdateSemesterRequest
        {
            Name = null, // Should be ignored
            StartDate = new DateTime(2023, 10, 1)
        };

        mapper.UpdateEntity(request, semester);

        // Name is preserved (no normalization because it was null in request)
        Assert.Equal("Fall", semester.Name);
        Assert.Equal(new DateTime(2023, 10, 1), semester.StartDate);
    }
}
