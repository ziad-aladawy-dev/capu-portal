using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;

namespace CapitalUniversity.Sync.Courses.Pull;

public sealed class CourseValidator : IRecordValidator<Course>
{
    public const int MaxCreditHours = 12;

    public bool IsValid(Course record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ExternallySourced.ExternalId))
        {
            error = "ExternalId is required (sync merge key).";
            return false;
        }

        if (LocalizedJson.IsEmpty(record.Code))
        {
            error = "Course code is required.";
            return false;
        }

        if (LocalizedJson.IsEmpty(record.Title))
        {
            error = "Course title is required.";
            return false;
        }

        if (record.CreditHours <= 0 || record.CreditHours > MaxCreditHours)
        {
            error = $"CreditHours must be in (0, {MaxCreditHours}].";
            return false;
        }

        error = null;
        return true;
    }
}
