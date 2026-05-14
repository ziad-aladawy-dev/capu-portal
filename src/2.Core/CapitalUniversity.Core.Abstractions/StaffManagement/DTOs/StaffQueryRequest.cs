namespace CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

public class StaffQueryRequest
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public bool? PasswordExpired { get; set; }

    public string? Role { get; set; }

    public string? JobTitle { get; set; }

    public Guid? StructureNodeId { get; set; }

    public Guid? ScopeNodeId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}