using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class StudentRequest : BaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;
    public decimal? AmountPaid { get; set; }
    public string? PaymentTransactionId { get; set; }

    public string SubmittedData { get; set; } = "{}";
    public int CurrentStepOrder { get; set; } = 0;

    public Guid? AssignedToStaffId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<RequestHistoryEntry> HistoryEntries { get; set; } = new List<RequestHistoryEntry>();
}