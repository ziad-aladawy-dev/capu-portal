using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

[Mapper]
public partial class AcademicPlanMapper
{
    public partial AcademicPlanResponse MapToResponse(AcademicPlan entity);
    public partial AcademicPlanCourseResponse MapToCourseResponse(AcademicPlanCourse entity);
    public partial AcademicPlan MapToEntity(CreateAcademicPlanRequest request);
}
