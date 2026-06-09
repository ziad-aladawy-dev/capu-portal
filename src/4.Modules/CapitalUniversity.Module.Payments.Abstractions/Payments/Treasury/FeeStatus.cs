namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Lifecycle of a <c>StudentFee</c>. Stored as int — values are stable; never
/// reorder.
/// </summary>
public enum FeeStatus
{
    Pending = 0,
    IncludedInOrder = 1,
    Paid = 2,
    Cancelled = 3,
    Refunded = 4,
}
