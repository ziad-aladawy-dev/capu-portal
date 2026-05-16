namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;

public class PermissionDto
{
    public string Module { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public PermissionScopeDto Scope { get; set; } = new();
}

public class PermissionScopeDto
{
    public bool IsGlobalStructural { get; set; }
    public Guid? StructureNodeId { get; set; }
    public string? StructureNodePath { get; set; }

    public bool IsGlobalYear { get; set; }
    public Guid? AcademicYearId { get; set; }

    public bool IsGlobalSemester { get; set; }
    public Guid? SemesterId { get; set; }
}
