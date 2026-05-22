namespace CapitalUniversity.Modules.Payments.Abstractions;

/// <summary>
/// Provider-reported state of a <see cref="PaymentTransaction"/>. Stored as
/// int — values are stable; never reorder.
/// </summary>
public enum PaymentTransactionStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
}
