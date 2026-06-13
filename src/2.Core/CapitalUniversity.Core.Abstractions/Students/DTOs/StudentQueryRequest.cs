using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

public class StudentQueryRequest
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public bool? PasswordExpired { get; set; }

    public Guid? FacultyId { get; set; }

    public Guid? ProgramId { get; set; }

    public Guid? LevelId { get; set; }

    public int? LevelOrder { get; set; }

    public Guid? ScopeNodeId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public Guid? SemesterId { get; set; }

    public string? SortBy { get; set; }

    public bool Ascending { get; set; } = true;

    public int Page { get; set; } = 1;

    int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, PagingConstants.MaxPageSize);
    }
}