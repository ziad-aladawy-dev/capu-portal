using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
namespace CapitalUniversity.Core.Domain.Authorization;

public class RolePermission : BaseEntity, IRolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    
    public Guid ServiceId { get; set; }
    public string Resource { get; private set; } = string.Empty;
    public ActionLevel Level { get; private set; }

    // NOTE: navigation retained for compatibility; may be reviewed during modular separation
    public Role Role { get; set; } = null!;
    public CapitalUniversity.Core.Domain.Services.Service Service { get; set; } = null!;
    public ICollection<RolePermissionScope> Scopes { get; set; } = new List<RolePermissionScope>();
}
