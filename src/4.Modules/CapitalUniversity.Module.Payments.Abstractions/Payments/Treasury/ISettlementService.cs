namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Idempotent settlement of an order by its MerchantOrderId. On Paid, creates
/// exactly one Payment per fee (unique FeeId guards duplicates), marks fees and
/// the order Paid, and publishes a FeePaidEvent per fee. On Failed/Expired,
/// releases fees back to Pending. Shared by the webhook and reconciliation paths.
/// </summary>
public interface ISettlementService
{
    /// <param name="reportedAmount">
    /// Gateway-reported settled amount (gross), when available. On a Paid outcome
    /// it is verified against the order total; a mismatch blocks settlement and
    /// raises an alert (no Payment is created).
    /// </param>
    Task SettleAsync(
        string merchantOrderId,
        SettlementOutcome outcome,
        TransactionType source,
        string rawPayload,
        decimal? reportedAmount = null,
        CancellationToken cancellationToken = default);
}
