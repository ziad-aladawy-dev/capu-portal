using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Schedule.Abstractions;

namespace CapitalUniversity.Modules.Schedule.Domain;

/// <summary>
/// One descriptive timetable row attached to a <c>CourseOffering</c>. Day-of-week
/// + campus-local start/end time + a kind label (lecture / lab / …). Optional
/// free-text location and notes provide display metadata; the Schedule module
/// does NOT validate rooms, de-conflict overlapping bookings, or expand
/// recurrence. Two weekly meetings = two rows.
///
/// <para>
/// Per the project's modularity rule, <see cref="CourseOfferingId"/> is a bare
/// <c>Guid</c> with no EF navigation and no real FK constraint — the same
/// "FK-by-id; no nav" pattern used everywhere else for cross-module roots.
/// Scope and existence checks against the parent offering live in
/// <c>ScheduleSlotService</c>, which calls <c>ICourseOfferingService</c>.
/// </para>
///
/// <para>
/// Invariants enforced on the entity itself (so no caller, including a future
/// Registration / room-booking module, can bypass them):
/// <list type="bullet">
///   <item><see cref="EndTime"/> &gt; <see cref="StartTime"/>.</item>
///   <item>Both times are <see cref="TimeOnly"/> values — campus-local, no zone.</item>
/// </list>
/// </para>
/// </summary>
public class ScheduleSlot : BaseEntity, IExternallySourced
{
    /// <summary>
    /// Composed sync-provenance block. <see cref="BaseEntity"/> inheritance is
    /// untouched; this property carries the upstream-merge-key + external-wins
    /// fields the sync write gateway needs.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    /// <summary>Owning offering. FK-by-id; no nav. Set at creation and never reassigned — move-by-delete-and-recreate.</summary>
    public Guid CourseOfferingId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>Campus-local start time. The timetable reads against the campus calendar — storing a zone would invite "the offering moved an hour during DST" bugs.</summary>
    public TimeOnly StartTime { get; private set; }

    /// <summary>Campus-local end time. Strictly greater than <see cref="StartTime"/> — enforced by <see cref="SetTimeRange"/>.</summary>
    public TimeOnly EndTime { get; private set; }

    public ScheduleSlotKind Kind { get; set; } = ScheduleSlotKind.Lecture;

    /// <summary>Free-text location label. Not parsed, not de-conflicted.</summary>
    public string? Location { get; set; }

    /// <summary>Free-text descriptive notes.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/>. Only callers holding the <c>Open</c>
    /// permission may reopen via <see cref="Reopen"/>.
    /// </summary>
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public void EnsureMutable()
    {
        if (IsClosed)
            throw new InvalidOperationException("Schedule slot is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new InvalidOperationException("Schedule slot is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new InvalidOperationException("Schedule slot is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Apply a new start/end pair atomically so the entity is never in a state
    /// where one side has been moved but the other has not. Rejects
    /// <paramref name="end"/> &lt;= <paramref name="start"/> — equality is also
    /// rejected because a zero-length slot is meaningless.
    /// </summary>
    public void SetTimeRange(TimeOnly start, TimeOnly end)
    {
        EnsureMutable();
        if (end <= start)
        {
            throw new InvalidOperationException("End time must be strictly greater than start time.");
        }
        StartTime = start;
        EndTime = end;
    }
}
