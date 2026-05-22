using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Domain.Authorization;

/// <summary>
/// One action grant inside a role: a role is permitted to perform <see cref="Action"/>
/// on a given <see cref="Resource"/>. Rows are per-action under the manifest-driven
/// model — granting <c>EditClose</c> writes one row per implied action (<c>View</c>,
/// <c>Insert</c>, <c>EditClose</c>) so runtime evaluation is a plain set lookup
/// with no ladder arithmetic.
/// </summary>
public class RolePermission : BaseEntity, IRolePermission
{
    public Guid RoleId { get; set; }
    public Guid ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;

    public Role Role { get; set; } = null!;
    public Resource Resource { get; set; } = null!;

    // For EF
    public RolePermission() { }

    public RolePermission(Guid roleId, Guid resourceId, string action)
    {
        RoleId = roleId;
        ResourceId = resourceId;
        Action = action;
    }
}
