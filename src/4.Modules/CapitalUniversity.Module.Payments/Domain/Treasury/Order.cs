using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// A payment attempt over one or more selected unpaid fees. Holds the
/// <see cref="MerchantOrderId"/> assigned by Treasury (the primary external
/// identifier) and coordinates settlement. The settlement transaction creates
/// one <see cref="Payment"/> per contained fee.
/// </summary>
public class Order : BaseEntity, ISoftDeletable
{
    public Guid StudentId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Created;

    public Gateway Gateway { get; set; }

    /// <summary>Treasury's order identifier. Unique once assigned; empty until initiation.</summary>
    public string MerchantOrderId { get; set; } = string.Empty;

    public string RedirectUrl { get; set; } = string.Empty;

    public string GatewaySessionRef { get; set; } = string.Empty;

    /// <summary>Σ of contained fee totals (before collection fees). decimal(18,2).</summary>
    public decimal BaseAmount { get; set; }

    /// <summary>
    /// Collection / handling fees added on top of <see cref="BaseAmount"/>.
    /// Defaults to 0; the field exists so collection fees can be applied without
    /// a schema change. decimal(18,2).
    /// </summary>
    public decimal CollectionFees { get; set; }

    /// <summary>Amount charged by Treasury = BaseAmount + CollectionFees. decimal(18,2).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Number of fee line items in the order (internal preference/reporting).</summary>
    public int ItemCount { get; set; }

    public string Currency { get; set; } = "EGP";

    public DateTime? ExpiresAt { get; set; }

    public ICollection<StudentFee> Fees { get; set; } = new List<StudentFee>();
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
