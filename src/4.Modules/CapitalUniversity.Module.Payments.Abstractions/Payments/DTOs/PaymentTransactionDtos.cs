using CapitalUniversity.Core.Domain.Payments;

namespace CapitalUniversity.Core.Abstractions.Payments.DTOs;

public class PaymentTransactionResponse
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentTransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Inbound payload from a provider integration (webhook handler, manual
/// confirmation). <see cref="IdempotencyKey"/> is the dedup primary —
/// retried deliveries with the same key return the existing transaction.
/// <see cref="RawPayloadJson"/> retains the provider's original message.
/// </summary>
public class RecordPaymentRequest
{
    public Guid InvoiceId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentTransactionStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string RawPayloadJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = string.Empty;
}
