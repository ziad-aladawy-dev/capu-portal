using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
namespace CapitalUniversity.Core.Domain.Authorization;

public class RolePermission : BaseEntity, IRolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    
    public Guid ServiceId { get; set; }
    public string Resource { get; set; } = string.Empty;
    public ActionLevel Level { get; set; }

    // NOTE: navigation retained for compatibility; may be reviewed during modular separation
    public Role Role { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public ICollection<RolePermissionScope> Scopes { get; set; } = new List<RolePermissionScope>();

    // For EF
    public RolePermission() { }

    public RolePermission(Guid roleId, Guid serviceId, string resource, ActionLevel level)
    {
        RoleId = roleId;
        ServiceId = serviceId;
        Resource = resource;
        Level = level;
    }
}
