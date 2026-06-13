using System.Text.Json;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Polls Treasury status for PendingPayment orders and routes the result through
/// <see cref="ISettlementService"/> (the same idempotent path as the webhook).
/// Applies a grace window (skip orders mid-flow), a per-run cap, and a failed-
/// attempt counter that escalates a persistently-unreachable order to an ops
/// alert. Orders still unpaid past their <c>ExpiresAt</c> are expired.
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly IOrderRepository _orders;
    private readonly ITreasuryClient _treasury;
    private readonly ISettlementService _settlement;
    private readonly CoreDbContext _db;
    private readonly TreasuryOptions _options;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        IOrderRepository orders,
        ITreasuryClient treasury,
        ISettlementService settlement,
        CoreDbContext db,
        IOptions<TreasuryOptions> options,
        ILogger<ReconciliationService> logger)
    {
        _orders = orders;
        _treasury = treasury;
        _settlement = settlement;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-Math.Max(0, _options.ReconciliationGraceMinutes));
        var batchSize = Math.Max(1, _options.ReconciliationBatchSize);
        var maxAttempts = Math.Max(1, _options.ReconciliationMaxAttempts);

        var pending = await _orders.GetPendingPaymentOlderThanAsync(cutoff, batchSize, cancellationToken);
        if (!pending.Any()) return 0;

        var processed = 0;
        // Parallelize external API calls with a cap (e.g., 5 concurrent calls)
        // to avoid sequential latency while not overwhelming the external gateway.
        using var semaphore = new SemaphoreSlim(5);
        var tasks = pending.Select(async order =>
        {
            if (string.IsNullOrEmpty(order.MerchantOrderId)) return null;

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var status = await _treasury.GetStatusAsync(order.Gateway, order.MerchantOrderId, cancellationToken);
                return new { Order = order, Status = status, Error = (Exception?)null };
            }
            catch (Exception ex)
            {
                return new { Order = order, Status = (TreasuryStatusResult?)null, Error = (Exception?)ex };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        // Process results sequentially to ensure DbContext thread safety.
        foreach (var result in results)
        {
            if (result == null) continue;
            var order = result.Order;

            if (result.Error != null)
            {
                order.ReconciliationAttempts++;
                await _db.SaveChangesAsync(cancellationToken);
                if (order.ReconciliationAttempts >= maxAttempts)
                {
                    _logger.LogError(result.Error,
                        "Reconciliation: order {MerchantOrderId} has failed {Attempts} consecutive status checks; flagged for manual intervention.",
                        order.MerchantOrderId, order.ReconciliationAttempts);
                }
                else
                {
                    _logger.LogWarning(result.Error,
                        "Reconciliation: status check failed for {MerchantOrderId} (attempt {Attempts}); will retry next tick.",
                        order.MerchantOrderId, order.ReconciliationAttempts);
                }
                continue;
            }

            var status = result.Status!;
            var outcome = TreasuryStatusMapper.Map(status.Status);
            var reportedAmount = status.Amount;
            var raw = JsonSerializer.Serialize(status);

            // Successful status check — clear the failure counter.
            if (order.ReconciliationAttempts != 0)
            {
                order.ReconciliationAttempts = 0;
                await _db.SaveChangesAsync(cancellationToken);
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
            else if (outcome == SettlementOutcome.Expired)
            {
                await _settlement.SettleAsync(order.MerchantOrderId, SettlementOutcome.Expired, TransactionType.StatusCheck, raw, null, cancellationToken);
                processed++;
            }
            else if (order.ExpiresAt.HasValue && order.ExpiresAt.Value < DateTime.UtcNow)
            {
                await _settlement.SettleAsync(order.MerchantOrderId, SettlementOutcome.Expired, TransactionType.StatusCheck, raw, null, cancellationToken);
                processed++;
            }
        }

        _logger.LogInformation("Reconciliation: examined {Count} pending order(s), acted on {Processed}.", pending.Count, processed);
        return processed;
    }
}
