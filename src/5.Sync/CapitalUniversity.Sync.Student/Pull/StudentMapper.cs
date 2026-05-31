using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Pull;

public sealed class StudentMapper : IRecordMapper<ExternalStudent, StudentEntity>
{
    public StudentEntity Map(ExternalStudent external)
    {
        ArgumentNullException.ThrowIfNull(external);

        return new StudentEntity
        {
            // Id intentionally not assigned — set by the writer (existing) or by EF
            // (new). The merge key is ExternalStudentId.
            ExternalStudentId = external.ExternalStudentId,
            FirstName = external.FirstName.Trim(),
            LastName = external.LastName.Trim(),
            Email = external.Email.Trim().ToLowerInvariant(),
            ExternalUpdatedAt = external.ExternalUpdatedAt,
            ExternalVersion = external.ExternalVersion,
            LastSyncedAt = DateTimeOffset.UtcNow,
            OriginSystem = "external"
        };
    }
}