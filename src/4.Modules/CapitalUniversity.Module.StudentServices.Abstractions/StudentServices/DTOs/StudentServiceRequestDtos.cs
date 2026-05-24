namespace CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

/// <summary>
/// Full request detail — surfaced when the student or staff opens a single
/// request. Includes the submitted field values + uploaded documents so the
/// review UI has everything in one payload.
/// </summary>
public class StudentServiceRequestResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid StudentServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;

    public ServiceRequestStatus CurrentStatus { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? AssignedStaffId { get; set; }

    public string? CancellationReason { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Invoice id created in the Payments module when the service required payment. Null otherwise.</summary>
    public Guid? PaymentReferenceId { get; set; }

    public IReadOnlyList<ServiceFieldValueResponse> FieldValues { get; set; } = Array.Empty<ServiceFieldValueResponse>();
    public IReadOnlyList<ServiceDocumentSubmissionResponse> Documents { get; set; } = Array.Empty<ServiceDocumentSubmissionResponse>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Compact listing row — drops the nested field/document collections to keep
/// list pages cheap. Returned by list/search queries on both the student and
/// staff sides.
/// </summary>
public class StudentServiceRequestSummaryResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid StudentServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public ServiceRequestStatus CurrentStatus { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public Guid? PaymentReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ServiceFieldValueResponse
{
    public Guid FieldDefinitionId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public DynamicFieldType FieldType { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class ServiceDocumentSubmissionResponse
{
    public Guid Id { get; set; }
    public Guid DocumentDefinitionId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Submission payload. <c>Files</c> carries the uploaded document metadata —
/// the actual bytes are streamed to the configured storage backend by the
/// API layer (existing project file-storage approach), then this DTO is
/// composed from the storage handles before reaching the service.
/// </summary>
public class SubmitStudentServiceRequestRequest
{
    public Guid StudentServiceId { get; set; }
    public IReadOnlyList<ServiceFieldValueInput> FieldValues { get; set; } = Array.Empty<ServiceFieldValueInput>();
    public IReadOnlyList<ServiceDocumentSubmissionInput> Files { get; set; } = Array.Empty<ServiceDocumentSubmissionInput>();
}

public class ServiceFieldValueInput
{
    public Guid FieldDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Metadata for a single uploaded document. <see cref="StoredFileName"/> and
/// <see cref="FilePath"/> are produced by the storage layer; the service does
/// not stream bytes itself.
/// </summary>
public class ServiceDocumentSubmissionInput
{
    public Guid DocumentDefinitionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class CancelStudentServiceRequestRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ApproveStudentServiceRequestRequest
{
    public string? Note { get; set; }
}

public class RejectStudentServiceRequestRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class MoveRequestWorkflowStateRequest
{
    public ServiceRequestStatus TargetStatus { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Filter / pagination / sort parameters shared by every request list query.
/// Default sort policy lives on the service side per the spec:
///   <list type="bullet">
///     <item>Pending lists: oldest first.</item>
///     <item>Everything else: newest first.</item>
///   </list>
/// </summary>
/// <remarks>
/// 3.10 — inherits <see cref="PagedQueryRequest"/> for <c>Page</c> /
/// <c>PageSize</c> / <c>Search</c>. The resource-specific <see cref="SortBy"/>
/// / <see cref="SortAscending"/> pair stays because the service's pending-vs-
/// non-pending default flip depends on the simpler boolean shape. New callers
/// may also use the base <c>Sort</c> string for forward compatibility; the
/// service prefers <c>SortBy</c> when set.
/// </remarks>
public class StudentServiceRequestListQuery : Core.Abstractions.Shared.Paging.PagedQueryRequest
{
    public ServiceRequestStatus? Status { get; set; }
    public Guid? StudentServiceId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public DateTime? SubmittedFrom { get; set; }
    public DateTime? SubmittedTo { get; set; }

    /// <summary>One of: <c>"createdAt"</c>, <c>"submittedAt"</c>, <c>"status"</c>. Defaults to <c>"createdAt"</c>.</summary>
    public string? SortBy { get; set; }

    /// <summary>True for ascending. Default depends on the caller's filter: pending lists default to ascending (oldest first), every other list to descending.</summary>
    public bool? SortAscending { get; set; }

    public StudentServiceRequestListQuery()
    {
        // Preserve resource-specific historical default (25 vs the global 20).
        PageSize = 25;
    }
}

public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
