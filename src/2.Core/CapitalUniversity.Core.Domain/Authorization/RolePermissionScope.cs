using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Authorization;

// NOTE: relationship structure may require future normalization review
public class RolePermissionScope : BaseEntity
{
    public Guid RolePermissionId { get; set; }
    public Guid? StructureNodeId { get; set; }

    public RolePermission RolePermission { get; set; } = null!;
}