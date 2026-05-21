using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.CourseOffering.Application.Validators;

public class CreateCourseOfferingValidator : AbstractValidator<CreateCourseOfferingRequest>
{
    public CreateCourseOfferingValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.StructureNodeId).NotEmpty();
        RuleFor(x => x.SectionCode)
            .NotEmpty()
            .MaximumLength(32)
            .WithMessage(LocalizedKeys.CourseOfferings.SectionCodeRequired);
        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0)
            .WithMessage(LocalizedKeys.CourseOfferings.CapacityNegative);
        RuleFor(x => x.ExternalSystemId!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.ExternalSystemId));
    }
}

public class UpdateCourseOfferingValidator : AbstractValidator<UpdateCourseOfferingRequest>
{
    public UpdateCourseOfferingValidator()
    {
        RuleFor(x => x.SectionCode!)
            .NotEmpty()
            .MaximumLength(32)
            .When(x => x.SectionCode is not null)
            .WithMessage(LocalizedKeys.CourseOfferings.SectionCodeRequired);
        RuleFor(x => x.Capacity!.Value)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Capacity.HasValue)
            .WithMessage(LocalizedKeys.CourseOfferings.CapacityNegative);
        RuleFor(x => x.ExternalSystemId!)
            .MaximumLength(128)
            .When(x => !string.IsNullOrEmpty(x.ExternalSystemId));
    }
}
