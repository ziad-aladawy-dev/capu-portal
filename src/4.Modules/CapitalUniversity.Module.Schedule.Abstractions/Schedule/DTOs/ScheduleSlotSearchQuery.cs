using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Modules.Schedule.Abstractions.DTOs;

/// <summary>
/// Cross-offering schedule-slot search. Sort whitelist: <c>dayOfWeek</c>,
/// <c>startTime</c>, <c>createdAt</c>. Default order: <c>dayOfWeek:asc,startTime:asc</c>.
/// </summary>
public class ScheduleSlotSearchQuery : PagedQueryRequest
{
    public Guid? CourseOfferingId { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public ScheduleSlotKind? Kind { get; set; }

    /// <summary>Inclusive lower bound on start time-of-day.</summary>
    public TimeOnly? From { get; set; }

    /// <summary>Exclusive upper bound on start time-of-day.</summary>
    public TimeOnly? To { get; set; }
}
