using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

// RequiredMappingStrategy.None: audit/soft-delete base props (Id, CreatedAt,
// UpdatedAt, IsDeleted, RowVersion) and unrelated FKs/navs are deliberately
// not surfaced through the API DTOs. The default strict strategy treats
// every missing match as a warning, so we relax it for the read-side
// projections and creation entity factory below.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None, AllowNullPropertyAssignment = false)]
public partial class AcademicPlanMapper
{
    public partial AcademicPlanResponse MapToResponse(AcademicPlan entity);
    public partial AcademicPlanCourseResponse MapToCourseResponse(AcademicPlanCourse entity);

    [MapProperty(nameof(CreateAcademicPlanRequest.Name), nameof(AcademicPlan.Name), Use = nameof(ForceNormalizeIncoming))]
    public partial AcademicPlan MapToEntity(CreateAcademicPlanRequest request);

    /// <summary>
    /// PATCH-style sparse update. Omitted (null) source fields are ignored;
    /// only provided values overwrite target state.
    /// </summary>
    public partial void ApplyUpdate(UpdateAcademicPlanRequest request, AcademicPlan entity);

    protected string ForceNormalizeIncoming(string value) => LocalizedJson.Normalize(value);
}
