namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// A receipt returned by <c>GET /api/payments/receipts</c> (inside the Treasury
/// BaseResponse <c>data</c>). Receipt ids are integers in the Treasury contract.
/// </summary>
public sealed class TreasuryReceiptDto
{
    /// <summary>Treasury's integer receipt id.</summary>
    public int ReceiptId { get; set; }

    public int ConnectionTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Price of a single receipt unit (Treasury-owned).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";

    public bool IsActive { get; set; } = true;

    /// <summary>Upstream last-modified stamp, if provided. Used for external-wins merge.</summary>
    public DateTime? UpdatedAt { get; set; }
}
