using CapitalUniversity.Modules.Schedule.Domain;

namespace CapitalUniversity.Modules.Schedule.Repositories;

public interface IScheduleSlotRepository
{
    Task<ScheduleSlot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All slots attached to one offering, ordered by day then start time. Suitable for a weekly-timetable render.</summary>
    Task<IReadOnlyList<ScheduleSlot>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    /// <summary>Returns true if a non-deleted slot already occupies the (offering, day, start, end) slot. Create-time precheck mirroring the DB unique index.</summary>
    Task<bool> ExistsAsync(Guid courseOfferingId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any existing slot for the same offering + day overlaps
    /// the proposed <c>[start, end)</c> half-open interval. Adjacency
    /// (existing.End == start, or end == existing.Start) is NOT a conflict —
    /// the inequality is strict by design so back-to-back slots like
    /// 10:00-11:00 and 11:00-12:00 coexist. Pass <paramref name="excludeId"/>
    /// on update to skip the row being edited; otherwise a no-op edit on the
    /// time would self-conflict.
    /// </summary>
    Task<bool> HasConflictAsync(
        Guid courseOfferingId,
        DayOfWeek dayOfWeek,
        TimeOnly start,
        TimeOnly end,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(ScheduleSlot slot, CancellationToken cancellationToken = default);

    void Update(ScheduleSlot slot);

    void Delete(ScheduleSlot slot);
}
