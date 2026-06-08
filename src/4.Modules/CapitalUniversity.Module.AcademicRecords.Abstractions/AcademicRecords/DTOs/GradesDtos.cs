namespace CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

/// <summary>
/// One course result inside a semester, projected for read APIs. Catalog course
/// + academic-term display fields are joined in by the repository so the client
/// never fans out to the Courses / Semesters / Registration modules (avoids
/// N+1). Used both standalone (semester grade details) and nested inside
/// <see cref="SemesterHistoryDto"/>.
/// </summary>
public class SemesterCourseDto
{
    /// <summary>The academic-result row id (this attempt's own identity).</summary>
    public Guid Id { get; set; }

    /// <summary>The backing registration attempt (<c>StudentRegisteredCourse</c>) this result grades.</summary>
    public Guid StudentRegisteredCourseId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>Catalog course code (e.g. <c>"CS101"</c>), joined from the Courses module.</summary>
    public string CourseCode { get; set; } = string.Empty;

    /// <summary>Catalog course title, joined from the Courses module.</summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>Credit hours of the catalog course, joined from the Courses module.</summary>
    public int CreditHours { get; set; }

    /// <summary>1-based attempt ordinal (from the registration attempt) — distinguishes repeats.</summary>
    public int AttemptNumber { get; set; }

    /// <summary>True when this is the most recent attempt of the course (synced flag).</summary>
    public bool IsLatestAttempt { get; set; }

    /// <summary>Letter grade as synced (e.g. <c>"A"</c>, <c>"B+"</c>). Null for non-completed attempts.</summary>
    public string? Grade { get; set; }

    /// <summary>Numeric score as synced. Null for non-completed attempts.</summary>
    public decimal? NumericScore { get; set; }

    public AcademicResultStatus Status { get; set; }

    /// <summary>Credits awarded by this attempt (synced; 0 for non-passing outcomes).</summary>
    public int CreditsEarned { get; set; }

    /// <summary>When the Sync Platform last refreshed this result (UTC). Null if never synced.</summary>
    public DateTime? SyncedAt { get; set; }
}

/// <summary>
/// A single academic term plus every graded result the student holds in it.
/// Returned by the semester-history query ordered Latest → Oldest. Semester
/// identity/ordering is sourced from the existing <c>Semester</c> hierarchy
/// (derived through <c>StudentRegisteredCourse</c>) — never duplicated here.
/// </summary>
public class SemesterHistoryDto
{
    public Guid SemesterId { get; set; }

    public string SemesterName { get; set; } = string.Empty;

    /// <summary>Owning academic year — used by clients to group terms under a year header.</summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>Term ordinal within its academic year (from <c>Semester.Order</c>).</summary>
    public int Order { get; set; }

    public IReadOnlyList<SemesterCourseDto> Courses { get; set; } = new List<SemesterCourseDto>();
}

/// <summary>
/// Synchronized academic-summary values for a student. Every figure is stored as
/// delivered by the upstream system — the portal does NOT calculate GPA, CGPA,
/// credits, or standing (out of scope per <c>docs/Grades/Grades_Model.md</c>).
/// </summary>
public class AcademicSummaryDto
{
    public Guid StudentId { get; set; }

    public decimal Gpa { get; set; }

    public decimal Cgpa { get; set; }

    public int EarnedCredits { get; set; }

    public int RemainingCredits { get; set; }

    public int PassedHours { get; set; }

    public int FailedHours { get; set; }

    /// <summary>Synced academic standing label (e.g. <c>"Good Standing"</c>, <c>"Probation"</c>).</summary>
    public string AcademicStanding { get; set; } = string.Empty;

    /// <summary>When the Sync Platform last refreshed the summary (UTC). Null if never synced.</summary>
    public DateTime? SyncedAt { get; set; }
}
