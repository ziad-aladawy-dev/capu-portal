using System.Text.RegularExpressions;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;

using CoreStaff = CapitalUniversity.Core.Domain.Identity.Staff;

namespace CapitalUniversity.Sync.Staff.Pull;

public sealed partial class StaffValidator : IRecordValidator<CoreStaff>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    public bool IsValid(CoreStaff record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ExternallySourced.ExternalId))
        {
            error = "ExternalId is required (sync merge key).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.EmployeeCode))
        {
            error = "EmployeeCode is required.";
            return false;
        }

        if (LocalizedJson.IsEmpty(record.Name))
        {
            error = "Name is required.";
            return false;
        }

        if (LocalizedJson.IsEmpty(record.JobTitle))
        {
            error = "JobTitle is required.";
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

        if (string.IsNullOrWhiteSpace(record.Role))
        {
            error = "Role is required.";
            return false;
        }

        error = null;
        return true;
    }
}
