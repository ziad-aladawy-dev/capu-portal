using System.Text.RegularExpressions;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;

// See StudentMapper.cs — Sync.Student namespace collides with Core's Student type.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Student.Pull;

public sealed partial class StudentValidator : IRecordValidator<CoreStudent>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    public bool IsValid(CoreStudent record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ExternallySourced.ExternalId))
        {
            error = "ExternalId is required (sync merge key).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.StudentCode))
        {
            error = "StudentCode is required.";
            return false;
        }

        // Name is bilingual JSON {"ar":"…","en":"…"} — IsNullOrWhiteSpace
        // would always pass on the JSON literal, so use LocalizedJson.IsEmpty.
        if (LocalizedJson.IsEmpty(record.Name))
        {
            error = "Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.NationalId))
        {
            error = "NationalId is required.";
            return false;
        }

        if (record.BirthDate == default)
        {
            error = "BirthDate is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.Email))
        {
            error = "Email is required.";
            return false;
        }

        if (!EmailPattern().IsMatch(record.Email))
        {
            error = "Email format invalid.";
            return false;
        }

        error = null;
        return true;
    }
}
