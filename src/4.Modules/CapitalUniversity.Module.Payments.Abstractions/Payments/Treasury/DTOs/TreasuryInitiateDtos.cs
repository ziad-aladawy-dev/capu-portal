namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>Billing details required by BM and eFinance initiation.</summary>
public sealed class TreasuryBillingDetails
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string? Currency { get; set; }
}

/// <summary>
/// Normalized initiation input. The client maps this to the gateway-specific
/// wire request (BM/Mastercard/eFinance). <see cref="ReceiptIds"/> is already
/// expanded per quantity (a quantity-N fee contributes its receipt id N times).
/// </summary>
public sealed class TreasuryInitiateRequest
{
    public List<int> ReceiptIds { get; set; } = new();
    public string StudentReferenceId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";

    /// <summary>Required for BM and eFinance; ignored by Mastercard.</summary>
    public TreasuryBillingDetails? Billing { get; set; }

    /// <summary>eFinance only: CARD, CHANNEL, MOBILE_WALLET, MEEZA, NOT_SET.</summary>
    public string? PaymentMechanism { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// Normalized initiation result. Each gateway response is mapped to this:
/// BM → MerchantOrderId = bmmId, RedirectUrl = checkoutUrl; Mastercard/eFinance
/// → their merchantOrderId + redirectUrl.
/// </summary>
public sealed class TreasuryInitiateResult
{
    public string MerchantOrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}

/// <summary>Portal-facing result of initiating payment for an order.</summary>
public class OrderInitiationResponse
{
    public Guid OrderId { get; set; }
    public string MerchantOrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}
