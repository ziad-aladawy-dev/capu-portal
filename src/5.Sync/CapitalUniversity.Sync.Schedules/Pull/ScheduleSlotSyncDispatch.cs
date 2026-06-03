using CapitalUniversity.Modules.Schedule.Domain;

namespace CapitalUniversity.Sync.Schedules.Pull;

/// <summary>
/// Pipeline carrier: a mapped Core <see cref="ScheduleSlot"/> + the upstream
/// course-offering key that <see cref="ScheduleSlotWriter"/> resolves to
/// <c>ScheduleSlot.CourseOfferingId</c> via the gateway before persisting.
/// </summary>
public sealed class ScheduleSlotSyncDispatch
{
    public required ScheduleSlot Entity { get; init; }
    public required string ExternalCourseOfferingId { get; init; }
}
