using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// Append-only audit of every interaction with a Treasury gateway (initiate /
/// webhook / status check / refund). Distinct from the legacy
/// <c>CapitalUniversity.Modules.Payments.Domain.PaymentTransaction</c> (which
/// is left untouched); this type maps to the <c>TreasuryPaymentTransactions</c>
/// table. Never edited after insertion.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Treasury order identifier this interaction concerns.</summary>
    public string MerchantOrderId { get; set; } = string.Empty;

    public Gateway Gateway { get; set; }

    public TransactionType Type { get; set; }

    public GatewayTransactionStatus Status { get; set; }

    /// <summary>Amount reported by the gateway; null for status checks. decimal(18,2).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Gateway's own reference for this interaction.</summary>
    public string GatewayReference { get; set; } = string.Empty;

    /// <summary>Raw gateway payload — store-and-forward, never parsed for business logic.</summary>
    public string RawResponse { get; set; } = "{}";

    /// <summary>De-dup key. Unique per <see cref="MerchantOrderId"/>.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
