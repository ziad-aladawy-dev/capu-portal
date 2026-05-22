using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Authorization;

/// <summary>
/// A permissible thing inside a <see cref="Module"/>. <c>Module → Resources → Actions</c>
/// is the canonical authorization shape: each module declares one or more Resources
/// in its <c>IPermissionManifest</c>; each Resource carries a stable <see cref="Key"/>
/// slug (e.g. <c>"invoices"</c>, <c>"academic-years"</c>) that matches the
/// <c>module.resource.action</c> identity used by <c>[HasPermission]</c> attributes
/// and the cached permission lookup. Persisted; rows are reconciled at startup by
/// <c>PermissionManifestSynchronizer</c>.
/// </summary>
public class Resource : BaseEntity
{
    public Guid ModuleId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public Module Module { get; set; } = null!;
}
