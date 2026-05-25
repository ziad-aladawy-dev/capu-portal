using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.StudentServices.Abstractions;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// A single student-submitted request against a configured
/// <see cref="StudentService"/>. Carries the submitted field values, the
/// uploaded documents, and the current workflow status.
///
/// <para>
/// <see cref="StudentId"/> is a cross-module reference (no EF navigation per
/// the modularity rule). The service-layer enforces the existence-leak
/// rule — out-of-scope or missing requests both surface as <c>NotFound</c>.
/// </para>
/// </summary>
public class StudentServiceRequest : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }

    public Guid StudentServiceId { get; set; }
    public StudentService? StudentService { get; set; }

    public ServiceRequestStatus CurrentStatus { get; set; } = ServiceRequestStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public Guid? AssignedStaffId { get; set; }

    public string? CancellationReason { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Invoice id created in the Payments module when the parent service charges a fee.</summary>
    public Guid? PaymentReferenceId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ServiceFieldValue> FieldValues { get; set; } = new List<ServiceFieldValue>();
    public ICollection<ServiceDocumentSubmission> Documents { get; set; } = new List<ServiceDocumentSubmission>();
}
