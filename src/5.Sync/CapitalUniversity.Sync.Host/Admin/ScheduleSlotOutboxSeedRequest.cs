using CapitalUniversity.Modules.Schedule.Abstractions;

namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/schedules/{externalScheduleSlotId}</c>. Field
/// names match the Core <c>ScheduleSlot</c> shape.
/// </summary>
public sealed class ScheduleSlotOutboxSeedRequest
{
    public string? ExternalCourseOfferingId { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public ScheduleSlotKind? Kind { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}
