namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Outcome of a gateway interaction recorded on a <c>PaymentTransaction</c>
/// audit row. Named distinctly from the legacy <c>PaymentTransactionStatus</c>
/// to avoid a clash while the old payment path still exists. Stored as int —
/// values are stable; never reorder.
/// </summary>
public enum GatewayTransactionStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}
