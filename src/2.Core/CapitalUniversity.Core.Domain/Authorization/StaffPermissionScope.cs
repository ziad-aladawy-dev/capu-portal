using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

// NOTE: relationship structure may require future normalization review
public class StaffPermissionScope : BaseEntity
{
    public Guid StaffPermissionId { get; set; }
    public Guid? StructureNodeId { get; set; }

    public StaffPermissionOverride StaffPermission { get; set; }
}