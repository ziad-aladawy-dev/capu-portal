using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.StudentServices.Application.Validators;

public class CreateStudentServiceValidator : AbstractValidator<CreateStudentServiceRequest>
{
    public CreateStudentServiceValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(64).WithMessage(LocalizedKeys.Infrastructure.Invalid)
            .Matches("^[a-z0-9][a-z0-9\\-]*$").WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(512);

        RuleFor(x => x.Description).MaximumLength(2048);

        RuleFor(x => x.EstimatedProcessingDays)
            .GreaterThanOrEqualTo(0).When(x => x.EstimatedProcessingDays.HasValue);

        RuleForEach(x => x.Fields).SetValidator(new ServiceFieldDefinitionValidator());
        RuleForEach(x => x.Documents).SetValidator(new ServiceDocumentDefinitionValidator());
    }
}

public class UpdateStudentServiceValidator : AbstractValidator<UpdateStudentServiceRequest>
{
    public UpdateStudentServiceValidator()
    {
        RuleFor(x => x.Name!).MaximumLength(512).When(x => x.Name is not null);
        RuleFor(x => x.Description!).MaximumLength(2048).When(x => x.Description is not null);
        RuleFor(x => x.EstimatedProcessingDays!.Value).GreaterThanOrEqualTo(0).When(x => x.EstimatedProcessingDays.HasValue);

        When(x => x.Fields is not null, () =>
        {
            RuleForEach(x => x.Fields!).SetValidator(new ServiceFieldDefinitionValidator());
        });
        When(x => x.Documents is not null, () =>
        {
            RuleForEach(x => x.Documents!).SetValidator(new ServiceDocumentDefinitionValidator());
        });
    }
}

public class ServiceFieldDefinitionValidator : AbstractValidator<CreateServiceFieldDefinitionRequest>
{
    public ServiceFieldDefinitionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(64)
            .Matches("^[A-Za-z][A-Za-z0-9_]*$").WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.Label).NotEmpty().MaximumLength(512);

        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        // Length bounds only make sense on text-ish fields.
        RuleFor(x => x.MaxLength)
            .GreaterThan(0).When(x => x.MaxLength.HasValue);
        RuleFor(x => x.MinLength)
            .GreaterThanOrEqualTo(0).When(x => x.MinLength.HasValue);
        RuleFor(x => x)
            .Must(x => !(x.MinLength.HasValue && x.MaxLength.HasValue) || x.MinLength!.Value <= x.MaxLength!.Value)
            .WithName(nameof(CreateServiceFieldDefinitionRequest.MaxLength))
            .WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x)
            .Must(x => !(x.MinValue.HasValue && x.MaxValue.HasValue) || x.MinValue!.Value <= x.MaxValue!.Value)
            .WithName(nameof(CreateServiceFieldDefinitionRequest.MaxValue))
            .WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.DropdownValues)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .When(x => x.FieldType == Abstractions.DynamicFieldType.Dropdown);
    }
}

public class ServiceDocumentDefinitionValidator : AbstractValidator<CreateServiceDocumentDefinitionRequest>
{
    public ServiceDocumentDefinitionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(64)
            .Matches("^[A-Za-z][A-Za-z0-9_]*$").WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.Label).NotEmpty().MaximumLength(512);

        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        RuleFor(x => x.AllowedExtensions)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(512);

        // Cap upload size at 50 MB by default — services that genuinely need
        // more can override per-document, but a missing cap is a footgun.
        RuleFor(x => x.MaxFileSizeBytes)
            .GreaterThan(0L).WithMessage(LocalizedKeys.Infrastructure.Invalid)
            .LessThanOrEqualTo(50L * 1024L * 1024L).WithMessage(LocalizedKeys.Infrastructure.Invalid);
    }
}
