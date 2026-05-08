using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class StaffPermissionOverride : BaseEntity, IUserPermissionOverride
{
    public Guid StaffId { get; set; }
    public Guid ServiceId { get; set; }
    public string Resource { get; private set; } = string.Empty;
    public ActionLevel Level { get; private set; }
    public string Domain { get; private set; } = string.Empty;
    public string Year { get; private set; } = string.Empty;
    public string Semester { get; private set; } = string.Empty;
    public OverrideType Type { get; private set; }
    public DateTime? ExpiresAt { get; set; }

    // NOTE: navigation retained for compatibility; may be reviewed during modular separation
    public Staff Staff { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public ICollection<StaffPermissionScope> Scopes { get; set; } = new List<StaffPermissionScope>();
}