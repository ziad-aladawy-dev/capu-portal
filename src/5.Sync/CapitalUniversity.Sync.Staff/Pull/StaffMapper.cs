using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Pull;

public sealed class StaffMapper : IRecordMapper<ExternalStaff, StaffEntity>
{
    public StaffEntity Map(ExternalStaff external)
    {
        ArgumentNullException.ThrowIfNull(external);

        return new StaffEntity
        {
            ExternalStaffId = external.ExternalStaffId,
            FirstName = external.FirstName.Trim(),
            LastName = external.LastName.Trim(),
            Email = external.Email.Trim().ToLowerInvariant(),
            Department = external.Department.Trim(),
            ExternalUpdatedAt = external.ExternalUpdatedAt,
            ExternalVersion = external.ExternalVersion,
            LastSyncedAt = DateTimeOffset.UtcNow,
            OriginSystem = "external"
        };
    }
}