using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Pull;

/// <summary>
/// Maps upstream <see cref="ExternalCourse"/> into Core's <see cref="Course"/>.
/// Title persists as bilingual JSON (Core's convention via
/// <c>CourseMapper.ForceNormalizeIncoming</c>); Code is language-neutral plain
/// text — Core's entity setter trims/upper-cases it and the nvarchar(32)
/// unique-index column expects the bare code. The sync layer no longer has a
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

            Code = external.Code?.Trim() ?? string.Empty,
            Title = LocalizedJson.Normalize(external.Title?.Trim()),
            CreditHours = external.CreditHours,
            Category = external.Category,
            IsActive = external.IsActive,
        };
    }
}
