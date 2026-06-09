using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// Immutable record of a settled fee. Exactly one per fee — enforced by a
/// unique index on <see cref="FeeId"/>, which is the schema-level idempotency
/// guarantee: a duplicate webhook can never create a second payment for a fee.
/// Never edited or deleted (not <c>ISoftDeletable</c>; no update path).
/// </summary>
public class Payment : BaseEntity
{
    /// <summary>FK to the paid fee. UNIQUE — the idempotency guarantee.</summary>
    public Guid FeeId { get; set; }

    public Guid OrderId { get; set; }

    /// <summary>Settled amount = the fee's TotalAmount. decimal(18,2).</summary>
    public decimal Amount { get; set; }

    public Gateway Gateway { get; set; }

    public string MerchantOrderId { get; set; } = string.Empty;

    public DateTime PaidAt { get; set; }
}
