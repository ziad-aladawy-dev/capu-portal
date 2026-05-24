using CapitalUniversity.Core.Abstractions.Shared.Paging;
using CapitalUniversity.Core.Domain.Courses;

namespace CapitalUniversity.Core.Abstractions.Courses.DTOs;

/// <summary>
/// Catalog course search. Sort whitelist: <c>code</c>, <c>creditHours</c>, <c>createdAt</c>.
/// Default order: <c>code:asc</c>.
/// </summary>
public class CourseSearchQuery : PagedQueryRequest
{
    public CourseCategory? Category { get; set; }
    public bool? IsActive { get; set; }
    public int? MinCreditHours { get; set; }
    public int? MaxCreditHours { get; set; }
}
