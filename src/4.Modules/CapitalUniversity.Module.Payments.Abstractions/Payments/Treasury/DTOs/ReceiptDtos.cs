namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

/// <summary>
/// A locally synced Treasury receipt. Unlike <see cref="TreasuryReceiptDto"/>
/// (live passthrough keyed by Treasury's integer id), this exposes the Portal's
/// own <see cref="Id"/> — the value fee rows reference and
/// <see cref="CreateServiceReceiptMappingRequest.ReceiptId"/> expects.
/// </summary>
public class ReceiptResponse
{
    public Guid Id { get; set; }
    public string ExternalReceiptId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public bool IsActive { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}
