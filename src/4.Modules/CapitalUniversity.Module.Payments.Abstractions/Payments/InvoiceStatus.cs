namespace CapitalUniversity.Modules.Payments.Abstractions;

/// <summary>
/// Lifecycle states for an <see cref="Invoice"/>. Stored as int — values are
/// stable; never reorder.
/// </summary>
public enum InvoiceStatus
{
    Pending = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Cancelled = 3,
    Refunded = 4,
}
