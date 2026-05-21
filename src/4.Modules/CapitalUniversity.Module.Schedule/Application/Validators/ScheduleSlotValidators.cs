using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.Schedule.Application.Validators;

public class CreateScheduleSlotValidator : AbstractValidator<CreateScheduleSlotRequest>
{
    public CreateScheduleSlotValidator()
    {
        RuleFor(x => x.CourseOfferingId).NotEmpty();

        RuleFor(x => x)
            .Must(r => r.EndTime > r.StartTime)
            .WithName(nameof(CreateScheduleSlotRequest.EndTime))
            .WithMessage(LocalizedKeys.Schedule.EndBeforeStart);

        RuleFor(x => x.Location!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.Notes!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}

public class UpdateScheduleSlotValidator : AbstractValidator<UpdateScheduleSlotRequest>
{
    public UpdateScheduleSlotValidator()
    {
        // Validate the start/end pair only when both are present in the partial
        // payload. When only one side is supplied, the service composes it
        // against the persisted value and runs the entity-level invariant —
        // a request that would make the slot zero- or negative-length
        // surfaces there as a Conflict.
        RuleFor(x => x)
            .Must(r => r.EndTime!.Value > r.StartTime!.Value)
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue)
            .WithName(nameof(UpdateScheduleSlotRequest.EndTime))
            .WithMessage(LocalizedKeys.Schedule.EndBeforeStart);

        RuleFor(x => x.Location!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.Notes!)
            .MaximumLength(512)
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
