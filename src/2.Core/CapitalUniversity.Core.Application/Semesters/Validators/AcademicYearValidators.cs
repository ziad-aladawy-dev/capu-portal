using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using FluentValidation;

namespace CapitalUniversity.Core.Application.Semesters.Validators;

public class CreateAcademicYearValidator : AbstractValidator<CreateAcademicYearRequest>
{
    public CreateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate)
            .WithMessage("EndDate must be greater than StartDate");
    }
}

public class UpdateAcademicYearValidator : AbstractValidator<(Guid Id, UpdateAcademicYearRequest Request)>
{
    public UpdateAcademicYearValidator()
    {
        RuleFor(x => x.Request.Name).MaximumLength(100);
        RuleFor(x => x.Request.EndDate).GreaterThan(x => x.Request.StartDate.GetValueOrDefault())
            .When(x => x.Request.EndDate.HasValue && x.Request.StartDate.HasValue)
            .WithMessage("EndDate must be greater than StartDate");
    }
}
