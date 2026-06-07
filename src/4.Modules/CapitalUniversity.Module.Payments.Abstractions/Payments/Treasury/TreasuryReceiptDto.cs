namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Raw receipt as returned by <c>GET /api/payments/receipts</c>.
///
/// <para>
/// ASSUMPTION: the endpoint returns a JSON array of objects with these
/// (case-insensitive) fields. The exact Treasury contract is not yet confirmed
/// — field names / wrapper shape may need adjustment once the real schema is
/// available. Deserialisation uses web defaults (case-insensitive).
/// </para>
/// </summary>
public sealed class TreasuryReceiptDto
{
    /// <summary>Treasury's stable receipt identifier.</summary>
    public string Id { get; set; } = string.Empty;

    public int ConnectionTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Price per single unit (Treasury-owned).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";

    public bool IsActive { get; set; } = true;

    /// <summary>Upstream last-modified stamp, if provided. Used for external-wins merge.</summary>
    public DateTime? UpdatedAt { get; set; }
}
