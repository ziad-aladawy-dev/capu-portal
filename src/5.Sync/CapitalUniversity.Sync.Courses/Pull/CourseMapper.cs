using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Pull;

/// <summary>
/// Maps upstream <see cref="ExternalCourse"/> into Core's <see cref="Course"/>.
/// Code + Title persist as bilingual JSON (Core's existing convention via
/// <c>CourseMapper.ForceNormalizeIncoming</c>). The sync layer no longer has a
/// staging copy — the gateway writes straight to Core.
/// </summary>
public sealed class CourseMapper : IRecordMapper<ExternalCourse, Course>
{
    public Course Map(ExternalCourse external)
    {
        ArgumentNullException.ThrowIfNull(external);

        return new Course
        {
            ExternallySourced = new()
            {
                ExternalId = external.ExternalCourseId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            // Code's setter on Core.Course uppercases the value. With bilingual
            // JSON in play the setter would mangle the JSON shape — we let the
            // setter run anyway because (a) Core's existing CourseMapper does
            // the same, and (b) the read side normalises via LocalizedJson
            // before display. If Core's setter changes, this comment can drop.
            Code = LocalizedJson.Normalize(external.Code?.Trim()),
            Title = LocalizedJson.Normalize(external.Title?.Trim()),
            CreditHours = external.CreditHours,
            Category = external.Category,
            IsActive = external.IsActive,
        };
    }
}
