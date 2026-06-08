namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>
/// Request body for the gateway initiate endpoints. ASSUMPTION: Treasury accepts
/// receipt identifiers + a student reference + redirect URL and creates a payment
/// session. Exact field shape unconfirmed — adjust once the Treasury contract is
/// available.
/// </summary>
public class TreasuryInitiateRequest
{
    /// <summary>External (Treasury) receipt identifiers for the order's fees.</summary>
    public List<string> ReceiptIds { get; set; } = new();

    public string StudentReferenceId { get; set; } = string.Empty;

    public string RedirectUrl { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";
}

/// <summary>Response from the gateway initiate endpoints.</summary>
public class TreasuryInitiateResponse
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string? SessionReference { get; set; }
}

/// <summary>Portal-facing result of initiating payment for an order.</summary>
public class OrderInitiationResponse
{
    public Guid OrderId { get; set; }
    public string MerchantOrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}
