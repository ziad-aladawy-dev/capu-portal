using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

namespace CapitalUniversity.Modules.AcademicRecords.Abstractions;

/// <summary>
/// Read-only access to a student's synchronized academic outcomes — grade
/// history, per-semester grade detail, and the academic summary. The portal does
/// NOT calculate GPA/CGPA, manage registration, or enter/modify grades; this
/// module only stores and exposes what the Sync Platform delivers (see
/// <c>docs/Grades/Grades_Model.md</c>).
///
/// <para>
/// The codebase expresses its read side as service query-methods rather than
/// MediatR handlers (the one CQRS exception is the Roles module). The methods
/// below are the <c>GetStudentSemesterHistory</c>, <c>GetSemesterGrades</c>, and
/// <c>GetAcademicSummary</c> queries from the doc. Every method enforces that a
/// student may only read their own records (staff read within their
/// structure-node scope) via <c>IEffectiveScope</c>; out-of-scope reads return an
/// empty result rather than leaking existence.
/// </para>
/// </summary>
public interface IAcademicRecordsService
{
    /// <summary>
    /// All graded results for the student grouped by academic term, terms ordered
    /// Latest → Oldest. Supports large histories via a single projected query.
    /// </summary>
    Task<IReadOnlyList<SemesterHistoryDto>> GetStudentSemesterHistoryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detailed graded results for one academic term. Empty when the term has no
    /// results for the student or is out of the caller's scope.
    /// </summary>
    Task<IReadOnlyList<SemesterCourseDto>> GetSemesterGradesAsync(
        Guid studentId,
        Guid semesterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The student's latest synchronized academic summary, or <c>null</c> when no
    /// summary has synced yet or the caller is out of scope.
    /// </summary>
    Task<AcademicSummaryDto?> GetAcademicSummaryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
