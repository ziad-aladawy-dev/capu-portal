using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Application.Validators;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Schedule;

public class ScheduleSlotValidatorsTests
{
    private static CreateScheduleSlotRequest ValidCreate() => new()
    {
        CourseOfferingId = Guid.NewGuid(),
        DayOfWeek = DayOfWeek.Tuesday,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(10, 30),
        Kind = ScheduleSlotKind.Lecture,
        Location = "Bldg A, Room 201",
    };

    [Fact]
    public void Create_HappyPath_IsValid()
    {
        var result = new CreateScheduleSlotValidator().Validate(ValidCreate());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_MissingCourseOfferingId_Fails()
    {
        var req = ValidCreate();
        req.CourseOfferingId = Guid.Empty;

        var result = new CreateScheduleSlotValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.CourseOfferingId));
    }

    [Fact]
    public void Create_EndBeforeStart_Fails()
    {
        var req = ValidCreate();
        req.StartTime = new TimeOnly(12, 0);
        req.EndTime = new TimeOnly(11, 0);

        var result = new CreateScheduleSlotValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.EndTime));
    }

    [Fact]
    public void Create_EndEqualsStart_Fails()
    {
        var req = ValidCreate();
        req.StartTime = new TimeOnly(11, 0);
        req.EndTime = new TimeOnly(11, 0);

        var result = new CreateScheduleSlotValidator().Validate(req);
        result.IsValid.Should().BeFalse(
            "a zero-length slot must not pass the validator any more than the entity invariant accepts it");
    }

    [Fact]
    public void Update_AllNull_IsValid()
    {
        // Partial update DTO; an empty payload poking nothing must validate
        // so callers can update one field at a time.
        var result = new UpdateScheduleSlotValidator().Validate(new UpdateScheduleSlotRequest());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_OnlyStartProvided_IsValid_DeferredToService()
    {
        // When only one side of the range is supplied, the validator cannot
        // judge whether end > start — that requires the persisted value. The
        // service composes the pair and runs the entity invariant; the
        // validator must not block the call here.
        var result = new UpdateScheduleSlotValidator().Validate(new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(8, 0),
        });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_BothProvidedEndBeforeStart_Fails()
    {
        var result = new UpdateScheduleSlotValidator().Validate(new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(15, 0),
            EndTime = new TimeOnly(14, 0),
        });
        result.IsValid.Should().BeFalse(
            "when both sides are present the validator has full context and must reject end <= start eagerly");
    }

    // ----------------------------------------------------------------------
    // Length boundary tests — pin the exact MaximumLength thresholds. Without
    // these, mutations like "MaximumLength(128) → 127" or "(512) → 511" survive.
    // ----------------------------------------------------------------------

    [Fact]
    public void Create_LocationExactly128_IsValid()
    {
        var req = ValidCreate();
        req.Location = new string('L', 128);

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_LocationExceeds128_Fails()
    {
        var req = ValidCreate();
        req.Location = new string('L', 129);

        var result = new CreateScheduleSlotValidator().Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(req.Location));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_LocationAbsent_SkipsLengthRule(string? location)
    {
        var req = ValidCreate();
        req.Location = location;

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_NotesExactly512_IsValid()
    {
        var req = ValidCreate();
        req.Notes = new string('N', 512);

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_NotesExceeds512_Fails()
    {
        var req = ValidCreate();
        req.Notes = new string('N', 513);

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_NotesAbsent_SkipsLengthRule(string? notes)
    {
        var req = ValidCreate();
        req.Notes = notes;

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_EndOneMinuteAfterStart_IsValid()
    {
        // The strict-> guard's tightest passing case — one-minute slot. Pins
        // that the > is strict and not >=, and that the > side is end (not start).
        var req = ValidCreate();
        req.StartTime = new TimeOnly(9, 0);
        req.EndTime = new TimeOnly(9, 1);

        new CreateScheduleSlotValidator().Validate(req).IsValid.Should().BeTrue();
    }

    // ----- Update validator parity -----

    [Fact]
    public void Update_LocationExactly128_IsValid()
    {
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest { Location = new string('L', 128) })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_LocationExceeds128_Fails()
    {
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest { Location = new string('L', 129) })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_NotesExceeds512_Fails()
    {
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest { Notes = new string('N', 513) })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_OnlyEndProvided_IsValid_DeferredToService()
    {
        // Symmetry with the only-start-provided test: the validator can't
        // judge alone, so it must let the call through for the service.
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest { EndTime = new TimeOnly(10, 0) })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_EndOneMinuteAfterStart_IsValid()
    {
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest
            {
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 1),
            })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_EndEqualsStart_Fails()
    {
        new UpdateScheduleSlotValidator()
            .Validate(new UpdateScheduleSlotRequest
            {
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(11, 0),
            })
            .IsValid.Should().BeFalse();
    }
}