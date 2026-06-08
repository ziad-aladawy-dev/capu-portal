namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Periodically polls Treasury for PendingPayment orders and settles or expires
/// them through <see cref="ISettlementService"/> (same idempotent path as the
/// webhook). Backstops missed/late webhook delivery.
/// </summary>
public interface IReconciliationService
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
