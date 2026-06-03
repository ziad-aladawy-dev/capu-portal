using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxValidator : IRecordValidator<ScheduleSlotOutboxDispatch>
{
    public bool IsValid(ScheduleSlotOutboxDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Payload.ExternalScheduleSlotId))
        {
            error = "Outbox payload missing ExternalScheduleSlotId.";
            return false;
        }

        if (!string.Equals(record.Row.ExternalScheduleSlotId, record.Payload.ExternalScheduleSlotId, StringComparison.Ordinal))
        {
            error = "Outbox payload ExternalScheduleSlotId mismatch.";
            return false;
        }

        error = null;
        return true;
    }
}
