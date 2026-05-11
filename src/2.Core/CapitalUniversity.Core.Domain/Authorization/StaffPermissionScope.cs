using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

// NOTE: relationship structure may require future normalization review
public class StaffPermissionScope : BaseEntity
{
    public Guid StaffPermissionId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }

    public CapitalUniversity.Core.Domain.Users.StaffPermissionOverride StaffPermission { get; set; }
}