namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>
/// Response from the gateway status endpoints. ASSUMPTION: a free-text
/// <c>Status</c> string mapped to <c>SettlementOutcome</c> by the Portal.
/// Exact Treasury contract unconfirmed.
/// </summary>
public class TreasuryStatusResponse
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Inbound webhook payload. ASSUMPTION: carries MerchantOrderId + a status
/// string. Signature/auth is verified at the controller via a shared secret.
/// </summary>
public class TreasuryWebhookNotification
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
