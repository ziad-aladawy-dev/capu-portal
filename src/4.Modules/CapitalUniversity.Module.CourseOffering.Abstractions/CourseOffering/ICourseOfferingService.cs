using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;

namespace CapitalUniversity.Modules.CourseOffering.Abstractions;

/// <summary>
/// Owns the runtime availability view of a catalog course for one academic
/// term + one structure-node target. Does NOT own: registration orchestration,
/// schedule conflict logic, fee logic, transcript logic, prerequisite
/// resolution. Those live in their own modules and consume offerings by id.
/// </summary>
public interface ICourseOfferingService
{
    Task<CourseOfferingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cross-node paged search. Out-of-scope rows are filtered out of the
    /// returned page (count reflects the post-filter set so totals match what
    /// the caller can actually see).
    /// </summary>
    Task<PagedResult<CourseOfferingResponse>> SearchAsync(CourseOfferingSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Slim list for a given (structureNode, semester). Optional <paramref name="status"/> narrows to a single lifecycle stage. No N+1 — single query.</summary>
    Task<IReadOnlyList<CourseOfferingResponse>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>All sections of one course running in one term, across structure-node targets the caller may access.</summary>
    Task<IReadOnlyList<CourseOfferingResponse>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateCourseOfferingRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateCourseOfferingRequest request, CancellationToken cancellationToken = default);

    Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default);

    Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-activates a slate of offerings (Draft → Open). Each row is processed
    /// independently: a single illegal transition (already Closed, Cancelled, or
    /// out-of-scope) lands in <see cref="BulkActionResult.Failures"/> without
    /// rolling back peers that already committed.
    /// </summary>
    Task<BulkActionResult> BulkPublishAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-cancels a slate of offerings. Each row is processed independently
    /// (see <see cref="BulkPublishAsync"/>). The reason is accepted for audit
    /// purposes but not persisted on the entity yet — Phase-2 audit work owns
    /// the storage decision. Cancelling an already-cancelled or out-of-scope
    /// offering lands in <see cref="BulkActionResult.Failures"/>.
    /// </summary>
    Task<BulkActionResult> BulkCancelAsync(IReadOnlyList<Guid> ids, string reason, CancellationToken cancellationToken = default);
}
