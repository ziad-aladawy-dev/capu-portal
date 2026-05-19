using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using FluentValidation;

namespace CapitalUniversity.Core.Application.Courses.Validators;

public class CreateAcademicPlanValidator : AbstractValidator<CreateAcademicPlanRequest>
{
    public CreateAcademicPlanValidator()
    {
        RuleFor(x => x.StructureNodeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.EffectiveTo!.Value)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo.HasValue)
            .WithMessage(LocalizedKeys.Courses.EffectiveToAfterFrom);
    }
}

public class UpdateAcademicPlanValidator : AbstractValidator<(Guid Id, UpdateAcademicPlanRequest Request)>
{
    public UpdateAcademicPlanValidator()
    {
        RuleFor(x => x.Request.Name!)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Request.Name));
    }
}

public class AddPlanCourseValidator : AbstractValidator<AddPlanCourseRequest>
{
    public AddPlanCourseValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Level).InclusiveBetween(1, 10);
        RuleFor(x => x.Semester).InclusiveBetween(1, 4);
    }
}
