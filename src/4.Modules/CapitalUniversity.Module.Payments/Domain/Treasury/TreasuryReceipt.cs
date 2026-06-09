using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// A billable item maintained by the HU Treasury System and pulled into the
/// Portal as read-mostly reference data. Treasury owns the price — the Portal
/// never edits <see cref="UnitAmount"/> through business flows; it is refreshed
/// only by the receipt sync. Only <c>ConnectionTypeId == 6</c> receipts are
/// relevant to the Portal.
/// </summary>
public class TreasuryReceipt : BaseEntity, ISoftDeletable, IExternallySourced
{
    /// <summary>Sync-provenance + merge-key block. <c>ExternalId</c> mirrors <see cref="ExternalReceiptId"/>.</summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    /// <summary>Treasury's own stable receipt identifier.</summary>
    public string ExternalReceiptId { get; set; } = string.Empty;

    /// <summary>External financial source discriminator. Only 6 is persisted.</summary>
    public int ConnectionTypeId { get; set; }

    /// <summary>Display name (e.g. "Credit Hour", "Transcript Fee").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Treasury-owned price per single unit. decimal(18,2).</summary>
    public decimal UnitAmount { get; set; }

    /// <summary>ISO-4217 currency code.</summary>
    public string Currency { get; set; } = "EGP";

    public bool IsActive { get; set; } = true;
}
