using CapitalUniversity.Modules.Schedule.Abstractions;

namespace CapitalUniversity.Sync.Schedules.Domain;

/// <summary>
/// External shape from the upstream scheduling system. Maps to Core's
/// <c>Modules.Schedule.Domain.ScheduleSlot</c>. <see cref="ExternalCourseOfferingId"/>
/// is the upstream offering key — the gateway resolves it to
/// <c>ScheduleSlot.CourseOfferingId</c>.
/// </summary>
public sealed class ExternalScheduleSlot
{
    public required string ExternalScheduleSlotId { get; init; }
    public required string ExternalCourseOfferingId { get; init; }
    public required DayOfWeek DayOfWeek { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }
    public required ScheduleSlotKind Kind { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}
