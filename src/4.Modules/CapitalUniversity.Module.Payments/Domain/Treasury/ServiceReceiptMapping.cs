using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Modules.Payments.Domain.Treasury;

/// <summary>
/// Links a StudentServices service to the Treasury receipt that prices it.
/// Replaces service-owned pricing: the Portal resolves the mapping at fee-
/// generation time and reads the price from the receipt. Cross-module student-
/// service reference is id-only (no EF navigation, per the modularity rule).
/// </summary>
public class ServiceReceiptMapping : BaseEntity, ISoftDeletable
{
    public Guid StudentServiceId { get; set; }

    public Guid ReceiptId { get; set; }
    public TreasuryReceipt? Receipt { get; set; }

    public QuantityMode QuantityMode { get; set; } = QuantityMode.Fixed;

    public bool IsActive { get; set; } = true;
}
