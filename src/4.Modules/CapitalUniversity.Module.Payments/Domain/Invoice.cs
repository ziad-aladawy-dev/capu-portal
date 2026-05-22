using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Abstractions;

namespace CapitalUniversity.Modules.Payments.Domain;

/// <summary>
/// Financial obligation for a single student. Composed of one or more
/// <see cref="InvoiceItem"/> rows authored by other modules through the
/// <c>IFeeCreationService</c> contract — Payments owns the invoice schema and
/// totals math, never the upstream fee policy.
///
/// <para>
/// <see cref="TotalAmount"/> is denormalised for fast list queries; it is
/// always equal to the sum of item amounts at persist time (enforced by
/// <c>InvoiceService.Recalculate</c>).
/// </para>
/// </summary>
public class Invoice : BaseEntity, ISoftDeletable
{
    /// <summary>Cross-module reference to the owning student. No EF navigation per modularity rule.</summary>
    public Guid StudentId { get; set; }

    /// <summary>SQL Server <c>rowversion</c>. EF maps this to optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    /// <summary>Sum of item amounts at persist time. Stored as decimal(18,2).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Currency code, ISO-4217. School-wide single-currency for now; declared on the row for future expansion.</summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>Optional payable-by stamp. Service-level enforcement only.</summary>
    public DateTime? DueAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
