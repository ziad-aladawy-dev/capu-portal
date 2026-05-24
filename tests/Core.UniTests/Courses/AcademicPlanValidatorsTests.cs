using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Application.Courses.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Courses;

/// <summary>
/// Task 2 mutation-resistance suite for the AcademicPlan validators.
///
/// The three validators all hold thin FluentValidation chains, which Stryker
/// mutates aggressively (boundary flips on length / range, operator flips on
/// the EffectiveTo &gt; EffectiveFrom rule, negation of the `When` predicate
/// on the sparse-update name guard). The tests below pin each rule by
/// asserting both sides of every boundary so a one-character mutation flips
/// at least one assertion.
/// </summary>
public class AcademicPlanValidatorsTests
{
    // ---------------- CreateAcademicPlanValidator ----------------

    [Fact]
    public void Create_RequiresStructureNodeId()
    {
        var validator = new CreateAcademicPlanValidator();
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.Empty,
            Name = "BSc",
            EffectiveFrom = new DateTime(2025, 9, 1),
        });
        result.ShouldHaveValidationErrorFor(r => r.StructureNodeId);
    }

    [Fact]
    public void Create_RequiresNonEmptyName()
    {
        var validator = new CreateAcademicPlanValidator();
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "",
            EffectiveFrom = new DateTime(2025, 9, 1),
        });
        result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Theory]
    [InlineData(200, true)]   // exactly at the boundary — allowed
    [InlineData(201, false)]  // one over — rejected
    public void Create_NameLengthBoundary_IsInclusive200(int length, bool isValid)
    {
        var validator = new CreateAcademicPlanValidator();
        var name = new string('x', length);
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = name,
            EffectiveFrom = new DateTime(2025, 9, 1),
        });

        // Catches a mutation flipping MaximumLength(200) to MaximumLength(199)
        // (would reject length 200) and a mutation flipping `<` to `<=` in
        // FluentValidation internals.
        if (isValid)
            result.ShouldNotHaveValidationErrorFor(r => r.Name);
        else
            result.ShouldHaveValidationErrorFor(r => r.Name);
    }

    [Fact]
    public void Create_RequiresEffectiveFrom()
    {
        var validator = new CreateAcademicPlanValidator();
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "BSc",
            EffectiveFrom = default,
        });
        result.ShouldHaveValidationErrorFor(r => r.EffectiveFrom);
    }

    [Fact]
    public void Create_EffectiveToEqualToFrom_IsRejected_GreaterThanIsStrict()
    {
        // The rule is `GreaterThan(EffectiveFrom)`, so equal dates must fail.
        // A mutation flipping `GreaterThan` to `GreaterThanOrEqualTo` would
        // accept equal dates → this assertion flips.
        var validator = new CreateAcademicPlanValidator();
        var when = new DateTime(2025, 9, 1);
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "BSc",
            EffectiveFrom = when,
            EffectiveTo = when,
        });
        result.ShouldHaveValidationErrorFor("EffectiveTo.Value");
    }

    [Fact]
    public void Create_EffectiveTo_OneDayAfterFrom_IsAccepted()
    {
        // Pins the lower boundary on the GreaterThan rule.
        var validator = new CreateAcademicPlanValidator();
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "BSc",
            EffectiveFrom = new DateTime(2025, 9, 1),
            EffectiveTo = new DateTime(2025, 9, 2),
        });
        result.ShouldNotHaveValidationErrorFor("EffectiveTo.Value");
    }

    [Fact]
    public void Create_EffectiveTo_Null_IsAccepted_RuleSkipsViaWhen()
    {
        // Pins the `.When(x => x.EffectiveTo.HasValue)` guard. A mutation that
        // removes the When would run the rule against a null EffectiveTo and
        // throw a NullReferenceException at runtime (catches the mutation by
        // TestValidate emitting an error or throwing).
        var validator = new CreateAcademicPlanValidator();
        var result = validator.TestValidate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "BSc",
            EffectiveFrom = new DateTime(2025, 9, 1),
            EffectiveTo = null,
        });
        result.IsValid.Should().BeTrue("null EffectiveTo must skip the GreaterThan rule entirely");
    }

    // ---------------- UpdateAcademicPlanValidator ----------------

    [Fact]
    public void Update_NullName_IsAccepted_WhenGuardSkipsTheRule()
    {
        // Sparse-update: omitted Name must NOT trigger MaximumLength. A
        // mutation negating the `When` predicate would force the rule and
        // produce a NullRefException or surprising failure.
        var validator = new UpdateAcademicPlanValidator();
        var result = validator.TestValidate((Guid.NewGuid(), new UpdateAcademicPlanRequest { Name = null }));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_EmptyName_IsAccepted_WhenGuardSkipsTheRule()
    {
        // The When predicate is `!string.IsNullOrEmpty(x.Request.Name)`. Mutating
        // the negation would force the MaximumLength rule on empty string,
        // which is still valid for MaximumLength(200) — so the real signal is
        // that mutating it to `string.IsNullOrEmpty` would skip the rule when
        // a non-empty name IS present. We pin the inverse instead: an empty
        // name passes the When guard and skips MaximumLength, IsValid stays
        // true.
        var validator = new UpdateAcademicPlanValidator();
        var result = validator.TestValidate((Guid.NewGuid(), new UpdateAcademicPlanRequest { Name = "" }));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Update_NameLength_RespectsMax200_OnNonNullName(int length, bool isValid)
    {
        var validator = new UpdateAcademicPlanValidator();
        var result = validator.TestValidate((Guid.NewGuid(),
            new UpdateAcademicPlanRequest { Name = new string('x', length) }));
        result.IsValid.Should().Be(isValid);
    }

    // ---------------- AddPlanCourseValidator ----------------

    [Fact]
    public void AddPlanCourse_RequiresCourseId()
    {
        var validator = new AddPlanCourseValidator();
        var result = validator.TestValidate(new AddPlanCourseRequest
        {
            CourseId = Guid.Empty, Level = 1, Semester = 1,
        });
        result.ShouldHaveValidationErrorFor(r => r.CourseId);
    }

    [Theory]
    [InlineData(0, false)]   // below min — rejected
    [InlineData(1, true)]    // exactly at min
    [InlineData(10, true)]   // exactly at max
    [InlineData(11, false)]  // above max — rejected
    public void AddPlanCourse_Level_InclusiveBetween_1_And_10(int level, bool isValid)
    {
        var validator = new AddPlanCourseValidator();
        var result = validator.TestValidate(new AddPlanCourseRequest
        {
            CourseId = Guid.NewGuid(), Level = level, Semester = 1,
        });
        if (isValid)
            result.ShouldNotHaveValidationErrorFor(r => r.Level);
        else
            result.ShouldHaveValidationErrorFor(r => r.Level);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void AddPlanCourse_Semester_InclusiveBetween_1_And_4(int semester, bool isValid)
    {
        var validator = new AddPlanCourseValidator();
        var result = validator.TestValidate(new AddPlanCourseRequest
        {
            CourseId = Guid.NewGuid(), Level = 1, Semester = semester,
        });
        if (isValid)
            result.ShouldNotHaveValidationErrorFor(r => r.Semester);
        else
            result.ShouldHaveValidationErrorFor(r => r.Semester);
    }
}
