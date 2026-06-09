namespace CapitalUniversity.Modules.AcademicRecords.Abstractions;

/// <summary>
/// Outcome of one course attempt as reported by the upstream academic system.
/// The portal never computes or transitions these — it stores the snapshot the
/// Sync Platform delivers (see <c>docs/Grades/Grades_Model.md</c>). Stored as
/// <c>int</c>; values are stable and never reordered, new values are additive.
///
/// <para>
/// Only <see cref="Passed"/> / <see cref="Failed"/> are "completed" outcomes
/// that carry a real letter grade. The remaining statuses are non-completed and
/// surface with <c>Grade: "-"</c> in transcript output (per the doc's
/// "Transcript Course Status Rules").
/// </para>
/// </summary>
public enum AcademicResultStatus
{
    /// <summary>Attempt is still ongoing — no terminal grade yet.</summary>
    InProgress = 0,

    /// <summary>Completed with a passing grade.</summary>
    Passed = 1,

    /// <summary>Completed with a failing grade. Still counts toward repeat history.</summary>
    Failed = 2,

    /// <summary>Student withdrew from the course after registration.</summary>
    Withdrawn = 3,

    /// <summary>Coursework left incomplete; no terminal grade recorded.</summary>
    Incomplete = 4,
}
