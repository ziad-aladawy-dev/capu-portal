namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// How quantity is determined when generating a <c>StudentFee</c> from a
/// receipt. <c>Fixed</c> forces quantity = 1 (e.g. a single transcript fee);
/// <c>CallerSupplied</c> lets the caller pass a count (e.g. credit hours).
/// Stored as int — values are stable; never reorder.
/// </summary>
public enum QuantityMode
{
    Fixed = 0,
    CallerSupplied = 1,
}
