using System.Text.Json;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.Student.Application.Validators;

public class UpsertStudentProfileRecordValidator : AbstractValidator<UpsertStudentProfileRecordRequest>
{
    public UpsertStudentProfileRecordValidator()
    {
        RuleFor(x => x.SchemaVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DataJson)
            .NotEmpty()
            .Must(BeValidJson)
            .WithMessage("DataJson must be a syntactically valid JSON document.");
        RuleFor(x => x.CustomCategoryKey)
            .NotEmpty()
            .MaximumLength(64)
            .When(x => x.Category == StudentProfileCategory.Custom)
            .WithMessage("CustomCategoryKey is required when Category is Custom.");
    }

    private static bool BeValidJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class VerifyStudentProfileRecordValidator : AbstractValidator<VerifyStudentProfileRecordRequest>
{
    public VerifyStudentProfileRecordValidator()
    {
        RuleFor(x => x.VerifiedBy).NotEmpty();
    }
}
