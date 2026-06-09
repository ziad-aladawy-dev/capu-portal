using CapitalUniversity.Modules.Registration.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Registration.Abstractions;

/// <summary>
/// Read-only access to course registrations synchronized from external academic
/// systems. The portal does NOT manage registration — this module never
/// registers, drops, or modifies enrollments, and owns no synchronization
/// logic. It is the authoritative Student ↔ Course relationship for the portal
/// and backs transcript / grades features.
///
/// <para>
/// The codebase expresses its read side as service query-methods rather than
/// MediatR handlers (the one CQRS exception is the Roles module). The three
/// methods below are the <c>GetRegisteredCourses</c>,
/// <c>GetStudentRegistrationHistory</c>, and <c>GetCourseAttempts</c> queries
/// from <c>docs/Courses/Registration_Model.md</c>. Every method enforces that a
/// student may only read their own registrations (staff read within their
/// structure-node scope) via <c>IEffectiveScope</c>; out-of-scope reads return
/// an empty result rather than leaking existence.
/// </para>
/// </summary>
public interface IStudentRegistrationService
{
    /// <summary>
    /// Currently active registrations (<see cref="RegistrationStatus.Enrolled"/>)
    /// for the student, ordered by course code. Single projected query — no N+1.
    /// </summary>
    Task<IReadOnlyList<RegisteredCourseDto>> GetRegisteredCoursesAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All historical registrations for the student grouped by academic term,
    /// terms ordered along the academic timeline. Supports large histories via a
    /// single projected query.
    /// </summary>
    Task<IReadOnlyList<SemesterRegistrationDto>> GetStudentRegistrationHistoryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every attempt the student made at one specific course, ordered by
    /// <see cref="DTOs.CourseAttemptDto.AttemptNumber"/> so repeat history reads
    /// chronologically.
    /// </summary>
    Task<IReadOnlyList<CourseAttemptDto>> GetCourseAttemptsAsync(
        Guid studentId,
        Guid courseId,
        CancellationToken cancellationToken = default);
}
