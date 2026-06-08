using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions.DTOs;
using CapitalUniversity.Modules.Registration.Repositories;

namespace CapitalUniversity.Modules.Registration.Application;

/// <summary>
/// Read-only query surface over the registration read-model. Deliberately
/// narrow: no registration/drop/modify, no waitlist, no synchronization — those
/// are out of this module's scope per <c>docs/Courses/Registration_Model.md</c>.
///
/// <para>
/// Row-level access is enforced through <see cref="IEffectiveScope.CanAccessStudentAsync"/>
/// before any data is returned: a student passes only for their own id; staff
/// pass when one of their authorized path prefixes covers the student's
/// structure node. Out-of-scope reads return an empty result (no existence
/// leak), consistent with the CourseOffering / StudentServiceRequest services.
/// </para>
/// </summary>
public class StudentRegistrationService : IStudentRegistrationService
{
    private readonly IStudentRegistrationRepository _registrations;
    private readonly IEffectiveScope _scope;

    public StudentRegistrationService(
        IStudentRegistrationRepository registrations,
        IEffectiveScope scope)
    {
        _registrations = registrations;
        _scope = scope;
    }

    public async Task<IReadOnlyList<RegisteredCourseDto>> GetRegisteredCoursesAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<RegisteredCourseDto>();
        }

        return await _registrations.GetActiveForStudentAsync(studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<SemesterRegistrationDto>> GetStudentRegistrationHistoryAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<SemesterRegistrationDto>();
        }

        return await _registrations.GetHistoryForStudentAsync(studentId, cancellationToken);
    }

    public async Task<IReadOnlyList<CourseAttemptDto>> GetCourseAttemptsAsync(
        Guid studentId,
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<CourseAttemptDto>();
        }

        return await _registrations.GetCourseAttemptsForStudentAsync(studentId, courseId, cancellationToken);
    }
}
