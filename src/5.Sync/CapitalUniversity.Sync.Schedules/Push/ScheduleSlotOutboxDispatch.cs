using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxDispatch
{
    public required ScheduleSlotOutboxEntity Row { get; init; }
    public required ExternalScheduleSlot Payload { get; init; }
}
