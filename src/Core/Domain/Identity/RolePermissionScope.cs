using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

// NOTE: relationship structure may require future normalization review
public class RolePermissionScope : BaseEntity
{
    public Guid RolePermissionId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }

    public RolePermission RolePermission { get; set; } = null!;
}