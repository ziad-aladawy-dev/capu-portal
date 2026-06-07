namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Lifecycle of an <c>Order</c> (a payment attempt over selected fees). Stored
/// as int — values are stable; never reorder.
/// </summary>
public enum OrderStatus
{
    Created = 0,
    PendingPayment = 1,
    Paid = 2,
    Failed = 3,
    Expired = 4,
    Refunded = 5,
}
