using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

// RequiredMappingStrategy.None: audit/soft-delete base props (Id, CreatedAt,
// UpdatedAt, IsDeleted, RowVersion) and unrelated FKs/navs are deliberately
// not surfaced through the API DTOs. The default strict strategy treats
// every missing match as a warning, so we relax it for the read-side
// projections and creation entity factory below.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AcademicPlanMapper
{
    public partial AcademicPlanResponse MapToResponse(AcademicPlan entity);
    public partial AcademicPlanCourseResponse MapToCourseResponse(AcademicPlanCourse entity);
    public partial AcademicPlan MapToEntity(CreateAcademicPlanRequest request);
}
