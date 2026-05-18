using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

[Mapper]
public partial class CourseMapper
{
    public partial CourseResponse MapToResponse(Course entity);
    public partial Course MapToEntity(CreateCourseRequest request);
}
