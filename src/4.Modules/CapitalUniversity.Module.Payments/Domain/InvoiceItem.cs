using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Payments;

/// <summary>
/// One line on an <see cref="Invoice"/>. Authored by an upstream module
/// through <c>IFeeCreationService</c>: <see cref="SourceModule"/> +
/// <see cref="ReferenceId"/> identify what the fee is for, but Payments
/// stays domain-agnostic — it does not interpret either field.
/// </summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>Line amount in the invoice currency. decimal(18,2).</summary>
    public decimal Amount { get; set; }

    /// <summary>Free-text fee classifier (e.g. <c>"Tuition"</c>, <c>"LabFee"</c>). Service-defined.</summary>
    public string FeeType { get; set; } = string.Empty;

    /// <summary>Module key that authored the fee (e.g. <c>"registration"</c>, <c>"library"</c>).</summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>Cross-module reference back to the originating row (e.g. enrollment ID). Untyped — Payments never resolves it.</summary>
    public Guid? ReferenceId { get; set; }

    public string Description { get; set; } = string.Empty;
}
