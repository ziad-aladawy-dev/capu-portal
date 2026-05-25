using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

// RequiredMappingStrategy.None: audit base props + IsActive default are
// intentionally absent from the DTOs. Both Code and Title are bilingual —
// stored as {"ar":"…","en":"…"} JSON and decoded against the current culture
// by CourseService.Localize on the read path.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None, AllowNullPropertyAssignment = false)]
public partial class CourseMapper
{
    public partial CourseResponse MapToResponse(Course entity);

    [MapProperty(nameof(CreateCourseRequest.Code), nameof(Course.Code), Use = nameof(ForceNormalizeIncoming))]
    [MapProperty(nameof(CreateCourseRequest.Title), nameof(Course.Title), Use = nameof(ForceNormalizeIncoming))]
    public partial Course MapToEntity(CreateCourseRequest request);

    /// <summary>
    /// PATCH-style sparse update. Omitted (null) source fields are ignored;
    /// only provided values overwrite target state.
    /// </summary>
    public partial void ApplyUpdate(UpdateCourseRequest request, Course entity);

    protected string ForceNormalizeIncoming(string value) => LocalizedJson.Normalize(value);
}
