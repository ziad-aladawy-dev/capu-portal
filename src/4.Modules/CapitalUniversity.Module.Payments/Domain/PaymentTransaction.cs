using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions;

namespace CapitalUniversity.Modules.Payments.Domain;

/// <summary>
/// Single recorded interaction with a payment provider for an
/// <see cref="Invoice"/>. <see cref="IdempotencyKey"/> is unique per
/// invoice — replaying the same upstream call (gateway webhook, retried POST)
/// never duplicates the row. <see cref="RawPayloadJson"/> retains the
/// provider's original message for audit.
/// </summary>
public class PaymentTransaction : BaseEntity, ISoftDeletable
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>Provider key (e.g. <c>"Fawry"</c>, <c>"Stripe"</c>). Free-text.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider's own transaction ID. Stored for support reconciliation.</summary>
    public string ProviderTransactionId { get; set; } = string.Empty;

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    /// <summary>Amount the provider reported settling. decimal(18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary>Raw provider payload — store-and-forward, never parsed for business logic.</summary>
    public string RawPayloadJson { get; set; } = "{}";

    /// <summary>De-duplication key — unique per <see cref="InvoiceId"/>.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
