namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Kind of gateway interaction recorded on a <c>PaymentTransaction</c> audit
/// row. Stored as int — values are stable; never reorder.
/// </summary>
public enum TransactionType
{
    Initiate = 0,
    Webhook = 1,
    StatusCheck = 2,
    Refund = 3,
}
