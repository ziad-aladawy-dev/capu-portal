using CapitalUniversity.Modules.Registration.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Registration.Repositories;

/// <summary>
/// Read-only data access for the registration read-model. Every method returns
/// fully-projected DTOs (catalog course + term display fields joined in a single
/// SQL query) so callers never fan out to the Courses / Semesters modules. There
/// are no write methods here on purpose: registration data is owned by the Sync
/// Platform, which persists through the generic <c>ICoreWriteGateway</c>.
/// </summary>
public interface IStudentRegistrationRepository
{
    /// <summary>Currently-enrolled registrations for the student, ordered by course code.</summary>
    Task<IReadOnlyList<RegisteredCourseDto>> GetActiveForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>All registrations for the student, grouped into terms ordered along the academic timeline.</summary>
    Task<IReadOnlyList<SemesterRegistrationDto>> GetHistoryForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    /// <summary>Every attempt the student made at one course, ordered by attempt number.</summary>
    Task<IReadOnlyList<CourseAttemptDto>> GetCourseAttemptsForStudentAsync(
        Guid studentId,
        Guid courseId,
        CancellationToken cancellationToken = default);
}
