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
}
