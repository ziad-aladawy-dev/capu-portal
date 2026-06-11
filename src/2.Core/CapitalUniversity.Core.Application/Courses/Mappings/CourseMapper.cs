using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Courses;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Courses.Mappings;

// RequiredMappingStrategy.None: audit base props + IsActive default are
// intentionally absent from the DTOs. Title is bilingual — stored as
// {"ar":"…","en":"…"} JSON and decoded against the current culture by
// CourseService.Localize on the read path. Code is language-neutral plain
// text (e.g. "CS101"): it maps verbatim because the entity setter
// trims/upper-cases it, CodeExistsAsync and the unique index compare it
// verbatim, and the column is nvarchar(32) — wrapping it in localized JSON
// would mangle the keys through the upper-casing setter and overflow the
// column.
//
// AutoUserMappings = false: without it Mapperly silently adopts
// ForceNormalizeIncoming as the default string→string conversion and runs
// EVERY string property (Code included) through it — the explicit MapProperty
// attribute below must stay the only place it applies.
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None, AllowNullPropertyAssignment = false, AutoUserMappings = false)]
public partial class CourseMapper
{
    public partial CourseResponse MapToResponse(Course entity);

    [MapProperty(nameof(CreateCourseRequest.Title), nameof(Course.Title), Use = nameof(ForceNormalizeIncoming))]
    public partial Course MapToEntity(CreateCourseRequest request);

    /// <summary>
    /// PATCH-style sparse update. Omitted (null) source fields are ignored;
    /// only provided values overwrite target state.
    /// </summary>
    [MapProperty(nameof(UpdateCourseRequest.Title), nameof(Course.Title), Use = nameof(ForceNormalizeIncoming))]
    public partial void ApplyUpdate(UpdateCourseRequest request, Course entity);

    protected string ForceNormalizeIncoming(string value) => LocalizedJson.Normalize(value);
}
