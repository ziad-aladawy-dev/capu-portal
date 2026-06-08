using System.Text.Json;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Polls Treasury status for PendingPayment orders and routes the result through
/// <see cref="ISettlementService"/> (the same idempotent path as the webhook).
/// Orders still unpaid past their <c>ExpiresAt</c> are expired (fees released).
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly IOrderRepository _orders;
    private readonly ITreasuryClient _treasury;
    private readonly ISettlementService _settlement;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        IOrderRepository orders,
        ITreasuryClient treasury,
        ISettlementService settlement,
        ILogger<ReconciliationService> logger)
    {
        _orders = orders;
        _treasury = treasury;
        _settlement = settlement;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _orders.GetPendingPaymentOlderThanAsync(DateTime.UtcNow, cancellationToken);
        var processed = 0;

        foreach (var order in pending)
        {
            if (string.IsNullOrEmpty(order.MerchantOrderId)) continue;

            SettlementOutcome outcome;
            string raw;
            decimal? reportedAmount;
            try
            {
                var status = await _treasury.GetStatusAsync(order.Gateway, order.MerchantOrderId, cancellationToken);
                outcome = TreasuryStatusMapper.Map(status.Status);
                reportedAmount = status.Amount;
                raw = JsonSerializer.Serialize(status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconciliation: status check failed for {MerchantOrderId}; will retry next tick.", order.MerchantOrderId);
                continue;
            }

            if (outcome == SettlementOutcome.Paid)
            {
                await _settlement.SettleAsync(order.MerchantOrderId, SettlementOutcome.Paid, TransactionType.StatusCheck, raw, reportedAmount, cancellationToken);
                processed++;
            }
            else if (outcome == SettlementOutcome.Failed)
            {
                await _settlement.SettleAsync(order.MerchantOrderId, SettlementOutcome.Failed, TransactionType.StatusCheck, raw, null, cancellationToken);
                processed++;
            }
            else if (order.ExpiresAt.HasValue && order.ExpiresAt.Value < DateTime.UtcNow)
            {
                await _settlement.SettleAsync(order.MerchantOrderId, SettlementOutcome.Expired, TransactionType.StatusCheck, raw, null, cancellationToken);
                processed++;
            }
            // else still genuinely pending — leave for the next tick.
        }

        _logger.LogInformation("Reconciliation: examined {Count} pending order(s), acted on {Processed}.", pending.Count, processed);
        return processed;
    }
}
