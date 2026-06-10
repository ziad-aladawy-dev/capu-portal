namespace CapitalUniversity.Modules.Schedule.Abstractions.DTOs;

public class ScheduleSlotResponse
{
    public Guid Id { get; set; }
    public Guid CourseOfferingId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Campus-local time of day. No time-zone — the timetable is read against the campus calendar, not UTC.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>Campus-local time of day. Must be strictly greater than <see cref="StartTime"/>.</summary>
    public TimeOnly EndTime { get; set; }

    public ScheduleSlotKind Kind { get; set; }

    /// <summary>Free-text location label (e.g. <c>"Bldg A, Room 201"</c>). The Schedule module does not validate or de-conflict rooms — it stores the string.</summary>
    public string? Location { get; set; }

    /// <summary>Free-text descriptive notes shown alongside the slot. Optional.</summary>
    public string? Notes { get; set; }

    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateScheduleSlotRequest
{
    public Guid CourseOfferingId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ScheduleSlotKind Kind { get; set; } = ScheduleSlotKind.Lecture;
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// All fields optional — only set fields are applied. <c>CourseOfferingId</c>
/// is intentionally not exposed: a slot belongs to one offering for its
/// lifetime. To move a slot, delete + recreate.
/// </summary>
public class UpdateScheduleSlotRequest
{
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public ScheduleSlotKind? Kind { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Bulk-create of multiple slots under one parent offering with partial-success
/// semantics: non-conflicting slots land while conflicting / duplicate / invalid
/// ones are reported per-row in the returned <c>BulkActionResult</c>. Overlap is
/// checked against existing siblings and against earlier slots committed within
/// the same request (first-writer-wins).
/// </summary>
public class BatchCreateScheduleSlotsRequest
{
    public Guid CourseOfferingId { get; set; }

    public IReadOnlyList<BatchScheduleSlotItem> Slots { get; set; } = Array.Empty<BatchScheduleSlotItem>();
}

/// <summary>Single slot row inside a batch — same shape as <see cref="CreateScheduleSlotRequest"/> minus the parent id (carried once on the batch).</summary>
public class BatchScheduleSlotItem
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ScheduleSlotKind Kind { get; set; } = ScheduleSlotKind.Lecture;
    public string? Location { get; set; }
    public string? Notes { get; set; }
}
