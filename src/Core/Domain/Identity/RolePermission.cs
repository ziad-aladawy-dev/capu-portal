using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class RolePermission : BaseEntity, IRolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    
    public Guid ServiceId { get; set; }
    public string Resource { get; private set; } = string.Empty;
    public ActionLevel Level { get; private set; }

    // NOTE: navigation retained for compatibility; may be reviewed during modular separation
    public Role Role { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public ICollection<RolePermissionScope> Scopes { get; set; } = new List<RolePermissionScope>();
}
