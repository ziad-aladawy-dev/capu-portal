using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class StudentRequest : BaseEntity
{
    public Guid StudentId { get; set; }
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RequestNumber { get; set; }

    [NotMapped]
    public string? StudentCode { get; set; }
    [NotMapped]
    public string? StudentNameJson { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotRequired;
    public decimal? AmountPaid { get; set; }
    public string? PaymentTransactionId { get; set; }

    public string SubmittedData { get; set; } = "{}";

    public int CurrentStepOrder { get; set; } = 0;

    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Guid? AssignedToStaffId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<RequestHistoryEntry> HistoryEntries { get; set; } = new List<RequestHistoryEntry>();
}