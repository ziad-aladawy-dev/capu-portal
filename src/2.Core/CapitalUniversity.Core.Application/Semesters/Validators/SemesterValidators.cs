using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using FluentValidation;

namespace CapitalUniversity.Core.Application.Semesters.Validators;

public class CreateSemesterValidator : AbstractValidator<CreateSemesterRequest>
{
    public CreateSemesterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate)
            .WithMessage(LocalizedKeys.Semesters.EndAfterStart);
    }
}

public class UpdateSemesterValidator : AbstractValidator<(Guid Id, UpdateSemesterRequest Request)>
{
    public UpdateSemesterValidator()
    {
        RuleFor(x => x.Request.Name).MaximumLength(100);
    }
}
