using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;

namespace CapitalUniversity.Modules.Schedule.Abstractions;

/// <summary>
/// Owns CRUD over the timetable rows attached to a <c>CourseOffering</c>. Does
/// NOT own: conflict detection (rooms / instructors / students), recurrence
/// expansion, registration impact analysis, or any orchestration across
/// offerings. Schedule is passive, descriptive metadata.
/// </summary>
public interface IScheduleSlotService
{
    Task<ScheduleSlotResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cross-offering paged search. Rows whose parent offering is invisible to the caller are filtered out.</summary>
    Task<PagedResult<ScheduleSlotResponse>> SearchAsync(ScheduleSlotSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>All slots attached to one offering, ordered by day then start time. Returns empty when the offering is not visible to the caller.</summary>
    Task<IReadOnlyList<ScheduleSlotResponse>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateScheduleSlotRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateScheduleSlotRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);

    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically create multiple slots under one parent offering. The whole
    /// batch commits together or none of it does — a partial schedule is
    /// rejected on the first overlap (intra-batch or against existing
    /// siblings) or per-slot validation failure. Returns the ids of the
    /// newly-created slots in input order.
    /// </summary>
    Task<IReadOnlyList<Guid>> BatchCreateAsync(BatchCreateScheduleSlotsRequest request, CancellationToken cancellationToken = default);
}
