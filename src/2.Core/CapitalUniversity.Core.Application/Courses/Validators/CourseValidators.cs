using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using FluentValidation;

namespace CapitalUniversity.Core.Application.Courses.Validators;

public class CreateCourseValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreditHours).InclusiveBetween(0, 12)
            .WithMessage(LocalizedKeys.Courses.CreditHoursOutOfRange);
    }
}

public class UpdateCourseValidator : AbstractValidator<(Guid Id, UpdateCourseRequest Request)>
{
    public UpdateCourseValidator()
    {
        RuleFor(x => x.Request.Title)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Request.Title));

        RuleFor(x => x.Request.CreditHours!.Value)
            .InclusiveBetween(0, 12)
            .When(x => x.Request.CreditHours.HasValue)
            .WithMessage(LocalizedKeys.Courses.CreditHoursOutOfRange);
    }
}
