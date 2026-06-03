using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Student.Domain;

// Alias the Core entity to dodge the namespace/type collision: the unqualified
// name `Student` already refers to the sibling namespace
// `CapitalUniversity.Sync.Student` from inside any sub-namespace of this
// project, so we cannot use a `Student` alias — pick a non-colliding name.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Student.Pull;

/// <summary>
/// Maps an upstream <see cref="ExternalStudent"/> into Core's Student entity
/// directly — the sync layer no longer has a staging duplicate. The merge
/// into Core happens through
/// <see cref="CapitalUniversity.Core.Abstractions.Sync.ICoreWriteGateway"/>;
/// this mapper just populates the columns sync owns.
/// <para>
/// Fields sync DOES NOT set on Student:
/// <list type="bullet">
///   <item><c>Id</c> — assigned by EF on insert; reused on update.</item>
///   <item><c>PasswordHash</c>, <c>PasswordExpiry</c>, <c>SessionVersion</c> — auth fields, not in sync's purview.</item>
///   <item><c>StructureNodeId</c> / <c>StructureNode</c> — out of sync scope per the platform plan.</item>
/// </list>
/// The writer's merge delegate enforces this on the update path; for new
/// students the writer uses <c>AllowInsert = false</c> so missing
/// <c>StructureNodeId</c> can't strand an orphan.
/// </para>
/// </summary>
public sealed class StudentMapper : IRecordMapper<ExternalStudent, CoreStudent>
{
    public CoreStudent Map(ExternalStudent external)
    {
        ArgumentNullException.ThrowIfNull(external);

        return new CoreStudent
        {
            // ExternallySourced — composed data block; the gateway uses
            // ExternalId as the merge key and stamps OriginSystem +
            // LastSyncedAt itself.
            ExternallySourced = new()
            {
                ExternalId = external.ExternalStudentId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            // Operational columns sync owns.
            StudentCode = external.StudentCode.Trim(),
            // Name is bilingual JSON {"ar":"…","en":"…"} in Core.
            Name = LocalizedJson.Normalize(external.Name?.Trim()),
            NationalId = external.NationalId.Trim(),
            BirthDate = external.BirthDate,
            PhoneNumber = external.PhoneNumber.Trim(),
            Email = external.Email.Trim().ToLowerInvariant(),
            IsActive = external.IsActive,

            // PasswordHash, StructureNodeId, StructureNode left at their
            // default values — the writer's merge delegate never touches them
            // and AllowInsert is off so inserts can't run without them.
        };
    }
}
