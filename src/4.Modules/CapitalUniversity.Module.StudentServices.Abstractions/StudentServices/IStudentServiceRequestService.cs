using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// Owns the request lifecycle. All write paths first validate the desired
/// status transition against the service's configured <c>WorkflowDefinition</c>
/// — invalid transitions surface as <c>ConflictException</c>. Scope checks
/// (student vs staff visibility) run on every read so the shared object cache
/// cannot leak across users.
/// </summary>
public interface IStudentServiceRequestService
{
    Task<StudentServiceRequestResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns requests visible to the caller (student → own requests; staff → role-scoped).</summary>
    Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListAsync(StudentServiceRequestListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns requests assigned to the calling staff member.</summary>
    Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListAssignedToStaffAsync(Guid staffId, StudentServiceRequestListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns the staff queue — pending / under-review items the caller is permitted to process. Default ordering: oldest first.</summary>
    Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListPendingAsync(StudentServiceRequestListQuery query, CancellationToken cancellationToken = default);

    Task<Guid> SubmitAsync(Guid studentId, SubmitStudentServiceRequestRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid studentId, Guid requestId, CancelStudentServiceRequestRequest request, CancellationToken cancellationToken = default);

    Task ApproveAsync(Guid requestId, Guid staffId, ApproveStudentServiceRequestRequest request, CancellationToken cancellationToken = default);

    Task RejectAsync(Guid requestId, Guid staffId, RejectStudentServiceRequestRequest request, CancellationToken cancellationToken = default);

    Task MoveStateAsync(Guid requestId, Guid staffId, MoveRequestWorkflowStateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms that the invoice attached to the request has been paid and
    /// moves the request from <see cref="ServiceRequestStatus.WaitingPayment"/>
    /// to the next configured state (typically <see cref="ServiceRequestStatus.UnderReview"/>).
    /// Designed to be called from the Payments module's webhook handler when a
    /// future outbox event ships; until then, callable directly by ops staff.
    /// </summary>
    Task ConfirmPaymentAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-transitions a slate of requests to <paramref name="payload"/>'s
    /// target status. Each row routes through <see cref="MoveStateAsync"/>, so
    /// per-row workflow validation, scope checks, and audit logging are all
    /// preserved. Independent per-id commits — a peer's failure does not roll
    /// back successes. Use case: staff queue review.
    /// </summary>
    Task<BulkActionResult> BulkTransitionAsync(IReadOnlyList<Guid> requestIds, Guid staffId, MoveRequestWorkflowStateRequest payload, CancellationToken cancellationToken = default);
}
