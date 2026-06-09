namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>
/// Normalized status result (from <c>GET /api/payments/{gateway}/status/{id}</c>).
/// </summary>
public sealed class TreasuryStatusResult
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Currency { get; set; } = "EGP";
}

/// <summary>
/// Inbound Treasury webhook payload. The transaction details (including
/// MerchantOrderId, status, and amounts) are nested under
/// <see cref="Transaction"/>.
/// </summary>
public sealed class TreasuryWebhookNotification
{
    public string? WebhookId { get; set; }
    public string? EventType { get; set; }
    public TreasuryWebhookTransaction? Transaction { get; set; }
}

public sealed class TreasuryWebhookTransaction
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? GrossAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = "EGP";
}
