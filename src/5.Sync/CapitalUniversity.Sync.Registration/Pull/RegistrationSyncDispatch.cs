using CapitalUniversity.Modules.Registration.Domain;

namespace CapitalUniversity.Sync.Registration.Pull;

/// <summary>
/// Pipeline carrier: a mapped Core <see cref="StudentRegisteredCourse"/> plus the
/// upstream student/course keys that <see cref="RegistrationWriter"/> resolves to
/// <c>StudentId</c> / <c>CourseId</c> via the gateway before persisting. Mirrors
/// <c>ScheduleSlotSyncDispatch</c>.
/// </summary>
public sealed class RegistrationSyncDispatch
{
    public required StudentRegisteredCourse Entity { get; init; }
    public required string ExternalStudentId { get; init; }
    public required string ExternalCourseId { get; init; }
}
