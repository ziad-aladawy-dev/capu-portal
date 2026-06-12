using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// A financial obligation assigned to a student, generated from a Treasury
/// receipt. <see cref="UnitAmount"/> is a frozen snapshot of the receipt price
/// at creation time; <see cref="TotalAmount"/> = <see cref="Quantity"/> ×
/// <see cref="UnitAmount"/>. A fee outlives the orders that fail to pay it, so
/// it is its own aggregate root — referenced (not owned) by <see cref="Order"/>.
/// </summary>
public class StudentFee : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }

    /// <summary>FK to the originating <see cref="TreasuryReceipt"/>.</summary>
    public Guid ReceiptId { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>Snapshot of the receipt price at creation. decimal(18,2).</summary>
    public decimal UnitAmount { get; set; }

    /// <summary>Quantity × UnitAmount, frozen at creation. decimal(18,2).</summary>
    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "EGP";

    public FeeStatus Status { get; set; } = FeeStatus.Pending;

    /// <summary>Module that requested the obligation (e.g. "student-services"). Opaque to Payments.</summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>Cross-module reference back to the originating row (e.g. service request id).</summary>
    public Guid? SourceReferenceId { get; set; }

    /// <summary>Set only while <see cref="FeeStatus.IncludedInOrder"/>; nulled on release.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>SQL Server <c>rowversion</c> — optimistic concurrency on the status flip.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public void EnsureMutable()
    {
        if (IsClosed)
            throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student fee is closed and cannot be modified.");
    }

    public void Close()
    {
        if (IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student fee is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student fee is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
