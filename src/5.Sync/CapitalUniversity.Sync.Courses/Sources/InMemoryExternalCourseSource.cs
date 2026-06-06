using System.Runtime.CompilerServices;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Sources;

/// <summary>
/// Deterministic in-memory simulator. Generates a catalog of <see cref="TotalCourses"/>
/// rows matching the field set of Core <c>Courses.Course</c>. Two rows (#4 and #12)
/// ship with blank titles so the validator drops them and warning aggregation can
/// be observed.
/// </summary>
public sealed class InMemoryExternalCourseSource : IExternalCourseSource
{
    public const int TotalCourses = 30;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 03, 01, 00, 00, 00, TimeSpan.Zero);

    private static readonly string[] SubjectCodes =
    {
        "MATH", "PHYS", "CHEM", "BIOL", "COMP",
        "ECON", "HIST", "LIT", "PHIL", "ENG"
    };

    private static readonly CourseCategory[] CategoryCycle =
    {
        CourseCategory.ProgramRequirement,
        CourseCategory.UniversityRequirement,
        CourseCategory.FacultyRequirement,
        CourseCategory.Elective,
        CourseCategory.GeneralEducation
    };

    private readonly IReadOnlyList<ExternalCourse> _store;

    public InMemoryExternalCourseSource()
    {
        var list = new List<ExternalCourse>(TotalCourses);
        for (var i = 1; i <= TotalCourses; i++)
        {
            var code = SubjectCodes[i % SubjectCodes.Length];
            var hasBlankTitle = i == 4 || i == 12;
            list.Add(new ExternalCourse
            {
                ExternalCourseId = $"EXT-C-{i:D4}",
                Code = $"{code}-{(100 + (i * 7) % 400)}",
                Title = hasBlankTitle ? string.Empty : $"Intro to {code} {i}",
                CreditHours = 3 + (i % 3),
                Category = CategoryCycle[i % CategoryCycle.Length],
                IsActive = true,
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalCourse> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var course in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || course.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return course;
                await Task.Yield();
            }
        }
    }
}
