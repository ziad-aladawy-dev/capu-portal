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

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/>.
    /// </summary>
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>
    /// Guards every mutating operation on the entity.
    /// </summary>
    public void EnsureMutable()
    {
        if (IsClosed)
            throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student request is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student request is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student request is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public ICollection<RequestHistoryEntry> HistoryEntries { get; set; } = new List<RequestHistoryEntry>();
    }