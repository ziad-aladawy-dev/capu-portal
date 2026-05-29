namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

public class StudentQueryRequest
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public bool? PasswordExpired { get; set; }

    public Guid? FacultyId { get; set; }

    public Guid? ProgramId { get; set; }

    public Guid? LevelId { get; set; }

    public Guid? ScopeNodeId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}