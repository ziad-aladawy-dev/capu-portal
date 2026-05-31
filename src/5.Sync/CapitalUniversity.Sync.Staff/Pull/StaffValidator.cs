using System.Text.RegularExpressions;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Pull;

public sealed partial class StaffValidator : IRecordValidator<StaffEntity>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    public bool IsValid(StaffEntity record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ExternalStaffId))
        {
            error = "ExternalStaffId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.FirstName) || string.IsNullOrWhiteSpace(record.LastName))
        {
            error = "Name is required.";
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

        if (string.IsNullOrWhiteSpace(record.Department))
        {
            error = "Department is required.";
            return false;
        }

        error = null;
        return true;
    }
}