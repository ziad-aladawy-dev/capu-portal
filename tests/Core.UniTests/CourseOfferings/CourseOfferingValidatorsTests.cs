using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.CourseOffering.Application.Validators;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

public class CourseOfferingValidatorsTests
{
    private static CreateCourseOfferingRequest ValidCreate() => new()
    {
        CourseId = Guid.NewGuid(),
        SemesterId = Guid.NewGuid(),
        StructureNodeId = Guid.NewGuid(),
        SectionCode = "A",
        Capacity = 30,
    };

    [Fact]
    public void Create_HappyPath_IsValid()
    {
        var result = new CreateCourseOfferingValidator().Validate(ValidCreate());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_MissingSectionCode_Fails()
    {
        var req = ValidCreate();
        req.SectionCode = string.Empty;

        var result = new CreateCourseOfferingValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.SectionCode));
    }

    [Fact]
    public void Create_NegativeCapacity_Fails()
    {
        var req = ValidCreate();
        req.Capacity = -1;

        var result = new CreateCourseOfferingValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.Capacity));
    }

    [Fact]
    public void Update_AllNull_IsValid()
    {
        // Partial-update DTO: every field is optional; an empty payload must
        // pass validation so callers can poke a single field at a time.
        var result = new UpdateCourseOfferingValidator().Validate(new UpdateCourseOfferingRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_NegativeCapacity_Fails()
    {
        var result = new UpdateCourseOfferingValidator().Validate(new UpdateCourseOfferingRequest { Capacity = -3 });
        result.IsValid.Should().BeFalse();
    }
}
