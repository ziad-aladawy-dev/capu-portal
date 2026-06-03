using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Staff.Domain;

// CapitalUniversity.Sync.Staff (this namespace) collides with Core's Staff
// type; alias to dodge the unqualified-name lookup.
using CoreStaff = CapitalUniversity.Core.Domain.Identity.Staff;

namespace CapitalUniversity.Sync.Staff.Pull;

public sealed class StaffMapper : IRecordMapper<ExternalStaff, CoreStaff>
{
    public CoreStaff Map(ExternalStaff external)
    {
        ArgumentNullException.ThrowIfNull(external);

        return new CoreStaff
        {
            ExternallySourced = new()
            {
                ExternalId = external.ExternalStaffId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            EmployeeCode = external.EmployeeCode.Trim(),
            // Bilingual JSON columns in Core.
            Name = LocalizedJson.Normalize(external.Name?.Trim()),
            JobTitle = LocalizedJson.Normalize(external.JobTitle?.Trim()),
            NationalId = external.NationalId.Trim(),
            BirthDate = external.BirthDate,
            PhoneNumber = external.PhoneNumber.Trim(),
            Email = external.Email.Trim().ToLowerInvariant(),
            Role = external.Role.Trim(),
            IsActive = external.IsActive,
            // PasswordHash, PasswordExpiry, SessionVersion, StructureNodeId left
            // unset — the writer's merge delegate never touches them, and the
            // gateway runs in update-only mode (AllowInsert = false) so we
            // never try to insert a row with these unset.
        };
    }
}
