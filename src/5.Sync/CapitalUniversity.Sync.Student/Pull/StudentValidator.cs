using System.Text.RegularExpressions;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Pull;

public sealed partial class StudentValidator : IRecordValidator<StudentEntity>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    public bool IsValid(StudentEntity record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ExternalStudentId))
        {
            error = "ExternalStudentId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.FirstName))
        {
            error = "FirstName is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.LastName))
        {
            error = "LastName is required.";
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