using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class StaffPermission : BaseEntity
{
    public Guid StaffId { get; set; }
    public Guid ServiceId { get; set; }
    public int Level { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public Staff Staff { get; set; }
    public Service Service { get; set; }

    public ICollection<StaffPermissionScope> Scopes { get; set; } = new List<StaffPermissionScope>();
}