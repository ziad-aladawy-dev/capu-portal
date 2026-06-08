using System.Runtime.CompilerServices;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Sync.Registration.Domain;

namespace CapitalUniversity.Sync.Registration.Sources;

/// <summary>
/// Deterministic in-memory simulator for local runs / tests. Generates
/// <see cref="TotalRegistrations"/> attempts spread across a handful of students
/// and courses, including a deliberate repeat (the same student + course in two
/// terms — attempts 1 and 2) so the repeat-history path can be observed, plus
/// one row with a blank <c>ExternalRegistrationId</c> so the validator's
/// merge-key guard is exercised (mirrors the blank-title rows in
/// <c>InMemoryExternalCourseSource</c>).
///
/// <para>
/// External course ids follow the <c>EXT-C-XXXX</c> scheme produced by
/// <c>InMemoryExternalCourseSource</c> so an end-to-end in-memory run (Courses
/// pulled first, then Registration) can resolve them. Semester / structure-node
/// ids are fixed placeholders — in a real deployment these are the portal's
/// seeded reference-data ids.
/// </para>
/// </summary>
public sealed class InMemoryExternalRegistrationSource : IExternalRegistrationSource
{
    public const int TotalRegistrations = 20;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new(2026, 03, 01, 00, 00, 00, TimeSpan.Zero);

    // Fixed placeholder reference-data ids — stand-ins for the portal's seeded
    // Semester / StructureNode rows.
    private static readonly Guid FallSemesterId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SpringSemesterId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProgramNodeId = new("33333333-3333-3333-3333-333333333333");

    private static readonly RegistrationStatus[] StatusCycle =
    {
        RegistrationStatus.Completed,
        RegistrationStatus.Enrolled,
        RegistrationStatus.Failed,
        RegistrationStatus.Withdrawn,
    };

    private readonly IReadOnlyList<ExternalRegistration> _store;

    public InMemoryExternalRegistrationSource()
    {
        var list = new List<ExternalRegistration>(TotalRegistrations);
        for (var i = 1; i <= TotalRegistrations; i++)
        {
            // Spread across 5 students and the 30-course catalog the Courses
            // simulator produces.
            var studentOrdinal = ((i - 1) % 5) + 1;
            var courseOrdinal = ((i - 1) % 12) + 1;
            var status = StatusCycle[i % StatusCycle.Length];

            // Row #7 ships without a merge key so the validator drops it.
            var hasBlankKey = i == 7;

            // Row #11 repeats #1's (student, course) in a later term as attempt 2
            // — the canonical "retook the course" case from the model doc.
            var isRepeat = i == 11;
            var studentForRow = isRepeat ? 1 : studentOrdinal;
            var courseForRow = isRepeat ? 1 : courseOrdinal;
            var attemptNumber = isRepeat ? 2 : 1;
            var semesterId = isRepeat || i % 2 == 0 ? SpringSemesterId : FallSemesterId;
            var completedAt = status is RegistrationStatus.Completed or RegistrationStatus.Failed
                ? BaselineUpdatedAt.AddMinutes(i).AddDays(120)
                : (DateTimeOffset?)null;

            list.Add(new ExternalRegistration
            {
                ExternalRegistrationId = hasBlankKey ? string.Empty : $"EXT-REG-{i:D4}",
                ExternalStudentId = $"EXT-S-{studentForRow:D4}",
                ExternalCourseId = $"EXT-C-{courseForRow:D4}",
                SemesterId = semesterId,
                StructureNodeId = ProgramNodeId,
                AttemptNumber = attemptNumber,
                Status = status,
                RegisteredAt = BaselineUpdatedAt.AddMinutes(i),
                CompletedAt = completedAt,
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1,
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalRegistration> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var registration in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || registration.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return registration;
                await Task.Yield();
            }
        }
    }
}
