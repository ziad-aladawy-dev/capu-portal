using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Schedules.Pull;

public sealed class ScheduleSlotValidator : IRecordValidator<ScheduleSlotSyncDispatch>
{
    public bool IsValid(ScheduleSlotSyncDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Entity.ExternallySourced.ExternalId))
        {
            error = "ExternalId is required (sync merge key).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.ExternalCourseOfferingId))
        {
            error = "ExternalCourseOfferingId is required (every slot attaches to an offering).";
            return false;
        }

        if ((int)record.Entity.DayOfWeek is < 0 or > 6)
        {
            error = "DayOfWeek must be in [0, 6] (0 = Sunday … 6 = Saturday).";
            return false;
        }

        // Entity invariant: SetTimeRange enforces End > Start at construction,
        // so by the time we reach the validator the times are already valid.
        // Empty-window checks are still cheap defense.
        if (record.Entity.EndTime <= record.Entity.StartTime)
        {
            error = "EndTime must be strictly after StartTime.";
            return false;
        }

        error = null;
        return true;
    }
}
