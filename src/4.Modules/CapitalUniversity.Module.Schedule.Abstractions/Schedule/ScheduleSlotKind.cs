namespace CapitalUniversity.Modules.Schedule.Abstractions;

/// <summary>
/// Descriptive label for what the slot is for — lecture, lab, etc. Stored as
/// int; values are stable and never reordered. Additive only. The Schedule
/// module does not interpret these for scheduling logic; they are metadata for
/// display and reporting.
/// </summary>
public enum ScheduleSlotKind
{
    Lecture = 0,
    Lab = 1,
    Tutorial = 2,
    Seminar = 3,
    Exam = 4,
    Other = 5,
}
