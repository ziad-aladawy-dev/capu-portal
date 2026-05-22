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

    // ----------------------------------------------------------------------
    // Boundary tests — pin the exact thresholds each rule enforces. Without
    // these, mutations like "MaximumLength(32) → 33" or ">= 0 → > 0" survive
    // because the existing tests only fire negatives well inside or far
    // outside the boundary.
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]   // lower edge: 0 must be valid (free / zero-seat draft)
    [InlineData(1)]   // immediately above the edge
    [InlineData(int.MaxValue)]
    public void Create_CapacityAtAndAboveZero_IsValid(int capacity)
    {
        var req = ValidCreate();
        req.Capacity = capacity;

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_SectionCodeExactly32_IsValid()
    {
        // 32 is the inclusive upper bound. A mutation that flips
        // MaximumLength(32) → MaximumLength(31) makes this fail; → 33 won't.
        var req = ValidCreate();
        req.SectionCode = new string('A', 32);

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_SectionCodeExceeds32_Fails()
    {
        var req = ValidCreate();
        req.SectionCode = new string('A', 33);

        var result = new CreateCourseOfferingValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.SectionCode));
    }

    [Fact]
    public void Create_SectionCodeWhitespaceOnly_Fails()
    {
        // NotEmpty + MaximumLength only catches null/empty/over-length, not
        // whitespace — but FluentValidation's NotEmpty treats whitespace as
        // empty for string. Pin the behavior so a future swap to NotNull (a
        // common refactor mistake) is caught.
        var req = ValidCreate();
        req.SectionCode = "   ";

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]                          // omitted entirely — rule's .When() skip
    [InlineData("")]                            // omitted via empty — rule's .When() skip
    public void Create_ExternalSystemIdAbsent_IsValid(string? external)
    {
        var req = ValidCreate();
        req.ExternalSystemId = external;

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_ExternalSystemIdExactly128_IsValid()
    {
        var req = ValidCreate();
        req.ExternalSystemId = new string('X', 128);

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_ExternalSystemIdExceeds128_Fails()
    {
        var req = ValidCreate();
        req.ExternalSystemId = new string('X', 129);

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_MissingCourseId_Fails()
    {
        var req = ValidCreate();
        req.CourseId = Guid.Empty;

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_MissingSemesterId_Fails()
    {
        var req = ValidCreate();
        req.SemesterId = Guid.Empty;

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_MissingStructureNodeId_Fails()
    {
        var req = ValidCreate();
        req.StructureNodeId = Guid.Empty;

        new CreateCourseOfferingValidator().Validate(req).IsValid.Should().BeFalse();
    }

    // ----- Update validator boundary parity with Create -----

    [Fact]
    public void Update_SectionCodeExactly32_IsValid()
    {
        new UpdateCourseOfferingValidator()
            .Validate(new UpdateCourseOfferingRequest { SectionCode = new string('A', 32) })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_SectionCodeExceeds32_Fails()
    {
        new UpdateCourseOfferingValidator()
            .Validate(new UpdateCourseOfferingRequest { SectionCode = new string('A', 33) })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_SectionCodeEmptyWhenProvided_Fails()
    {
        // The Update rule fires only when SectionCode != null; if provided as
        // empty string it must fail (a partial update cannot blank out a
        // required field).
        new UpdateCourseOfferingValidator()
            .Validate(new UpdateCourseOfferingRequest { SectionCode = "" })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_CapacityZero_IsValid()
    {
        new UpdateCourseOfferingValidator()
            .Validate(new UpdateCourseOfferingRequest { Capacity = 0 })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_ExternalSystemIdExceeds128_Fails()
    {
        new UpdateCourseOfferingValidator()
            .Validate(new UpdateCourseOfferingRequest { ExternalSystemId = new string('X', 129) })
            .IsValid.Should().BeFalse();
    }
}
