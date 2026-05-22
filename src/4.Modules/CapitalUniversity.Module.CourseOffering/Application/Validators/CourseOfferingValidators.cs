using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.CourseOffering.Application.Validators;

public class CreateCourseOfferingValidator : AbstractValidator<CreateCourseOfferingRequest>
{
    public CreateCourseOfferingValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required);
        RuleFor(x => x.SemesterId).NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required);
        RuleFor(x => x.StructureNodeId).NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required);
        RuleFor(x => x.SectionCode)
            .NotEmpty().WithMessage(LocalizedKeys.CourseOfferings.SectionCodeRequired)
            .MaximumLength(32).WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0)
            .WithMessage(LocalizedKeys.CourseOfferings.CapacityNegative);

        RuleFor(x => x.ExternalSystemId!)
            .MaximumLength(128).WithMessage(LocalizedKeys.Infrastructure.Invalid)
            .When(x => !string.IsNullOrEmpty(x.ExternalSystemId));
    }
}

public class UpdateCourseOfferingValidator : AbstractValidator<UpdateCourseOfferingRequest>
{
    public UpdateCourseOfferingValidator()
    {
        RuleFor(x => x.SectionCode)
            .NotEmpty().WithMessage(LocalizedKeys.CourseOfferings.SectionCodeRequired)
            .MaximumLength(32).WithMessage(LocalizedKeys.Infrastructure.Invalid)
            .When(x => x.SectionCode != null);

        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0)
            .WithMessage(LocalizedKeys.CourseOfferings.CapacityNegative)
            .When(x => x.Capacity.HasValue);

        RuleFor(x => x.ExternalSystemId!)
            .MaximumLength(128).WithMessage(LocalizedKeys.Infrastructure.Invalid)
            .When(x => !string.IsNullOrEmpty(x.ExternalSystemId));
    }
}

