using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Guid ServiceId { get; set; }
    public int Level { get; set; }

    public Role Role { get; set; }
    public Service Service { get; set; }

    public ICollection<RolePermissionScope> Scopes { get; set; } = new List<RolePermissionScope>();
}