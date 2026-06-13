using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
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

    /// <summary>Batch version — single SQL query for multiple offering IDs. No N+1.</summary>
    Task<IReadOnlyList<ScheduleSlotResponse>> GetForOfferingsAsync(IReadOnlyList<Guid> courseOfferingIds, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateScheduleSlotRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateScheduleSlotRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);

    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-create multiple slots under one parent offering with partial-success
    /// semantics (consistent with the Payments / CourseOffering bulk endpoints):
    /// each slot is validated and committed independently, so a conflicting,
    /// duplicate, or invalid slot is reported as a per-row failure in the
    /// returned <see cref="BulkActionResult"/> while its valid peers still land.
    /// A missing / out-of-scope parent offering rejects the whole request with
    /// <c>NotFoundException</c>.
    /// </summary>
    Task<BulkActionResult> BatchCreateAsync(BatchCreateScheduleSlotsRequest request, CancellationToken cancellationToken = default);
}
