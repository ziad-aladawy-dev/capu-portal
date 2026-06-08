using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

namespace CapitalUniversity.Modules.AcademicRecords.Repositories;

/// <summary>
/// Read-only data access for the academic-records read-model. Every method
/// returns fully-projected DTOs / carriers (academic result joined to its
/// registration attempt + catalog course + term in a single SQL query) so
/// callers never fan out to the Registration / Courses / Semesters modules.
/// There are no write methods: academic outcomes are owned by the Sync Platform,
/// which persists through the generic <c>ICoreWriteGateway</c>.
/// </summary>
public interface IAcademicRecordsRepository
{
    /// <summary>All graded results for the student, grouped into terms ordered Latest → Oldest.</summary>
    Task<IReadOnlyList<SemesterHistoryDto>> GetSemesterHistoryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>Graded results for the student in one term, ordered by course code.</summary>
    Task<IReadOnlyList<SemesterCourseDto>> GetSemesterGradesForStudentAsync(
        Guid studentId,
        Guid semesterId,
        CancellationToken cancellationToken = default);

    /// <summary>The student's latest synchronized academic summary, or null if none has synced.</summary>
    Task<AcademicSummaryDto?> GetLatestSummaryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The student's display identity (name + institutional code) for transcript
    /// headers, or null if no such student exists. Projected straight from the
    /// student record — no cross-module service round-trip.
    /// </summary>
    Task<StudentIdentity?> GetStudentIdentityAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Latest-attempt results for the student, each carrying its catalog
    /// <see cref="CourseCategory"/> and the structure node the registration was
    /// opened under — the raw material the transcript service maps into
    /// requirement categories. Ordered by course code.
    /// </summary>
    Task<IReadOnlyList<TranscriptSourceRow>> GetTranscriptRowsForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Course → is-mandatory map drawn from the active academic plan(s) of the
    /// given structure nodes. Used by the transcript service to split each
    /// requirement category into Compulsory vs Elective. Courses not present in
    /// any active plan are absent from the map.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> GetPlanMandatoryMapAsync(
        IReadOnlyCollection<Guid> structureNodeIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal carrier pairing a latest-attempt result's display fields with the
/// catalog category + structure node the transcript service needs to group it.
/// Not exposed to API clients — the service maps it into <c>TranscriptCourseDto</c>.
/// </summary>
public sealed class TranscriptSourceRow
{
    public Guid CourseId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public string CourseTitle { get; init; } = string.Empty;
    public int CreditHours { get; init; }
    public CourseCategory Category { get; init; }
    public Guid StructureNodeId { get; init; }
    public string? Grade { get; init; }
    public decimal? NumericScore { get; init; }
    public AcademicResultStatus Status { get; init; }
    public int CreditsEarned { get; init; }
}

/// <summary>Display identity for a transcript header — the student's name and institutional code.</summary>
public sealed class StudentIdentity
{
    public string Name { get; init; } = string.Empty;
    public string StudentCode { get; init; } = string.Empty;
}
