using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
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
            // M15 — Profile records are intentionally schemaless JSON blobs,
            // but the column is nvarchar(max). A naive client could land a
            // multi-MB document on the row, which then has to be reloaded for
            // every cached read. 64 KB is well above any realistic record
            // payload (the largest current category is a few thousand chars
            // worth of nested medical info) and well below the size that
            // starts hurting the cache layer.
            .MaximumLength(64_000)
            .Must(BeValidJson)
            .WithMessage(LocalizedKeys.StudentInformation.InvalidJson);
        RuleFor(x => x.CustomCategoryKey)
            .NotEmpty()
            .MaximumLength(64)
            .When(x => x.Category == StudentProfileCategory.Custom)
            .WithMessage(LocalizedKeys.StudentInformation.CustomCategoryKeyRequired);
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
