using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

/// <summary>
/// Student search query. Inherits <see cref="PagedQueryRequest"/> so it
/// participates in the shared paging/sort conventions (Phase 2 design,
/// adopted in Phase 3 item 3.10). Defaults preserved for backwards
/// compatibility (PageSize stays 10 vs the global 20 default).
/// </summary>
public class StudentQueryRequest : PagedQueryRequest
{
    public bool? IsActive { get; set; }

    public bool? PasswordExpired { get; set; }

    public Guid? FacultyId { get; set; }

    public Guid? ProgramId { get; set; }

    public Guid? LevelId { get; set; }

    public Guid? ScopeNodeId { get; set; }

    public StudentQueryRequest()
    {
        // Preserve the resource-specific historical default (10) — the global
        // base defaults to 20. Existing callers see no behavior change; the
        // global cap (MaxPageSize = 100) still applies.
        PageSize = 10;
    }
}
