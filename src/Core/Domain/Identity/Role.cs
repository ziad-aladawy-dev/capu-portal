using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class Role : BaseEntity
{
    public string Name { get; set; }
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<StaffRole> StaffRoles { get; set; } = new List<StaffRole>();
}