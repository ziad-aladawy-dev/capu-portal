using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

/// <summary>
/// Staff search query. Inherits <see cref="PagedQueryRequest"/> so it
/// participates in the shared paging/sort conventions (Phase 2 design,
/// adopted in Phase 3 item 3.10). Defaults preserved for backwards
/// compatibility (PageSize stays 10 vs the global 20 default).
/// </summary>
public class StaffQueryRequest : PagedQueryRequest
{
    public bool? IsActive { get; set; }

    public bool? PasswordExpired { get; set; }

    public string? Role { get; set; }

    public string? JobTitle { get; set; }

    public Guid? StructureNodeId { get; set; }

    public Guid? ScopeNodeId { get; set; }

    public StaffQueryRequest()
    {
        // Preserve resource-specific historical default. See StudentQueryRequest.
        PageSize = 10;
    }
}
