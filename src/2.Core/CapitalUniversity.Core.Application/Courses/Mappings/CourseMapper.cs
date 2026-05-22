using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

// RequiredMappingStrategy.None: audit base props + IsActive default are
// intentionally absent from the DTOs.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None, AllowNullPropertyAssignment = false)]
public partial class CourseMapper
{
    public partial CourseResponse MapToResponse(Course entity);
    public partial Course MapToEntity(CreateCourseRequest request);

    /// <summary>
    /// PATCH-style sparse update. Omitted (null) source fields are ignored;
    /// only provided values overwrite target state.
    /// </summary>
    public partial void ApplyUpdate(UpdateCourseRequest request, Course entity);
}
