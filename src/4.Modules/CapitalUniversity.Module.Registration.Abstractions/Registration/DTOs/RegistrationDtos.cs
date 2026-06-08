namespace CapitalUniversity.Modules.Registration.Abstractions.DTOs;

/// <summary>
/// One course-registration attempt projected for read APIs. Carries the catalog
/// course + academic-term display fields joined in by the repository so the
/// client never has to fan out to the Courses / Semesters modules (avoids N+1).
/// Used both standalone (current registrations) and nested inside
/// <see cref="SemesterRegistrationDto"/>.
/// </summary>
public class RegisteredCourseDto
{
    /// <summary>Registration row id (the attempt's own identity, not the course id).</summary>
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>Catalog course code (e.g. <c>"CS101"</c>), joined from the Courses module.</summary>
    public string CourseCode { get; set; } = string.Empty;

    /// <summary>Catalog course title, joined from the Courses module.</summary>
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>Credit hours of the catalog course, joined from the Courses module.</summary>
    public int CreditHours { get; set; }

    /// <summary>Academic term this attempt belongs to (the project's <c>Semester</c> entity).</summary>
    public Guid SemesterId { get; set; }

    /// <summary>Term display name, joined from the Semesters module.</summary>
    public string SemesterName { get; set; } = string.Empty;

    /// <summary>Organizational target (program/department) the registration was opened under.</summary>
    public Guid StructureNodeId { get; set; }

    /// <summary>1-based attempt ordinal — distinguishes repeats of the same course across terms.</summary>
    public int AttemptNumber { get; set; }

    public RegistrationStatus Status { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Upstream system's stable id for this registration (the sync merge key). Null for any non-synced row.</summary>
    public string? ExternalReferenceId { get; set; }

    /// <summary>When the Sync Platform last refreshed this row (UTC). Null if never synced.</summary>
    public DateTime? SyncedAt { get; set; }
}

/// <summary>
/// A single academic term plus every registration the student holds in it.
/// Returned by the registration-history query, ordered by the term's position
/// in the academic timeline. Semester identity/ordering is sourced from the
/// existing <c>Semester</c> hierarchy — never duplicated here.
/// </summary>
public class SemesterRegistrationDto
{
    public Guid SemesterId { get; set; }

    public string SemesterName { get; set; } = string.Empty;

    /// <summary>Owning academic year — used by clients to group terms under a year header.</summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>Term ordinal within its academic year (from <c>Semester.Order</c>).</summary>
    public int Order { get; set; }

    public IReadOnlyList<RegisteredCourseDto> Courses { get; set; } = new List<RegisteredCourseDto>();
}

/// <summary>
/// One attempt of a specific course, ordered by <see cref="AttemptNumber"/>.
/// Returned by the course-attempts query so a student can see every time they
/// took a given course (e.g. Calculus I in Fall 2022 and again in Fall 2023).
/// </summary>
public class CourseAttemptDto
{
    public Guid Id { get; set; }

    public Guid CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public Guid SemesterId { get; set; }

    public string SemesterName { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public RegistrationStatus Status { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
