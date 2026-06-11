using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.Events;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Idempotent settlement shared by the webhook and reconciliation paths.
/// Idempotency is schema-enforced: the unique index on <c>Payment.FeeId</c>
/// makes a duplicate notification structurally unable to create a second
/// payment for a fee. The audit-row unique <c>(MerchantOrderId, IdempotencyKey)</c>
/// dedups repeated identical outcomes. <see cref="IOutbox"/> is mandatory: every
/// host that runs settlement (API webhook + Sync reconciliation) must wire it so
/// FeePaidEvent is always delivered.
/// </summary>
public sealed class SettlementService : ISettlementService
{
    private readonly IOrderRepository _orders;
    private readonly IStudentFeeRepository _fees;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentTransactionRepository _transactions;
    private readonly CoreDbContext _db;
    private readonly ILogger<SettlementService> _logger;
    private readonly IOutbox _outbox;

    public SettlementService(
        IOrderRepository orders,
        IStudentFeeRepository fees,
        IPaymentRepository payments,
        IPaymentTransactionRepository transactions,
        CoreDbContext db,
        ILogger<SettlementService> logger,
        IOutbox outbox)
    {
        _orders = orders;
        _fees = fees;
        _payments = payments;
        _transactions = transactions;
        _db = db;
        _logger = logger;
        _outbox = outbox;
    }

    public async Task SettleAsync(
        string merchantOrderId,
        SettlementOutcome outcome,
        TransactionType source,
        string rawPayload,
        decimal? reportedAmount = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByMerchantOrderIdAsync(merchantOrderId, includeFees: true, cancellationToken: cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Settlement: no order for MerchantOrderId {MerchantOrderId}; ignoring.", merchantOrderId);
            return;
        }

        // H3 — A Paid notification whose reported amount does not match the order
        // total is recorded but NOT settled (the D9 block below). It must use a
        // DISTINCT idempotency key: if a benign mismatch consumed the canonical
        // "{source}:{outcome}" key, a later CORRECTED notification with the right
        // amount would be deduped here and the order could never settle.
        var isAmountMismatch = outcome == SettlementOutcome.Paid
            && reportedAmount.HasValue
            && reportedAmount.Value != order.TotalAmount;

        var idempotencyKey = isAmountMismatch
            ? $"{source}:{outcome}:amount-mismatch"
            : $"{source}:{outcome}";

        if (await _transactions.ExistsAsync(merchantOrderId, idempotencyKey, cancellationToken))
        {
            // This exact outcome (or this exact mismatch) was already processed.
            return;
        }

        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = merchantOrderId,
            Gateway = order.Gateway,
            Type = source,
            Status = isAmountMismatch
                ? GatewayTransactionStatus.Failed
                : outcome switch
                {
                    SettlementOutcome.Paid => GatewayTransactionStatus.Succeeded,
                    SettlementOutcome.Failed or SettlementOutcome.Expired => GatewayTransactionStatus.Failed,
                    _ => GatewayTransactionStatus.Pending,
                },
            // Record the REPORTED amount for a mismatch (aids reconciliation);
            // otherwise the order total.
            Amount = isAmountMismatch ? reportedAmount!.Value : order.TotalAmount,
            RawResponse = string.IsNullOrEmpty(rawPayload) ? "{}" : rawPayload,
            IdempotencyKey = idempotencyKey,
        }, cancellationToken);

        var paidFees = new List<StudentFee>();

        // D5 — a Paid notification settles an order awaiting payment OR one already
        // released (Failed/Expired): a late Paid is authoritative and re-opens the
        // order. Already Paid / Cancelled / Refunded are an idempotent no-op.
        var canSettlePaid = order.Status is OrderStatus.PendingPayment
                                          or OrderStatus.Failed
                                          or OrderStatus.Expired;

        if (outcome == SettlementOutcome.Paid && !canSettlePaid)
        {
            _logger.LogWarning(
                "Settlement: Paid notification for order {MerchantOrderId} in non-settleable state {Status}; ignored (idempotent).",
                merchantOrderId, order.Status);
        }
        else if (outcome == SettlementOutcome.Paid && reportedAmount.HasValue && reportedAmount.Value != order.TotalAmount)
        {
            // D9 — amount verification. A Paid notification whose reported amount
            // does not match the order total is NOT settled; raise an alert for
            // manual reconciliation (no Payment is created, order left as-is).
            _logger.LogWarning(
                "Settlement: reported paid amount {Reported} != order {MerchantOrderId} total {Total}; settlement blocked, manual reconciliation required.",
                reportedAmount.Value, merchantOrderId, order.TotalAmount);
        }
        else if (outcome == SettlementOutcome.Paid)
        {
            var wasReleased = order.Status != OrderStatus.PendingPayment;
            foreach (var fee in order.Fees)
            {
                // Guard: only settle fees still attached to THIS order. A released
                // fee that was re-claimed by a newer order now points elsewhere and
                // must not be paid twice (unique Payment.FeeId is the backstop).
                if (fee.OrderId != order.Id) continue;

                var existing = await _payments.GetByFeeIdAsync(fee.Id, cancellationToken);
                if (existing is null)
                {
                    await _payments.AddAsync(new Payment
                    {
                        FeeId = fee.Id,
                        OrderId = order.Id,
                        Amount = fee.TotalAmount,
                        Gateway = order.Gateway,
                        MerchantOrderId = merchantOrderId,
                        PaidAt = DateTime.UtcNow,
                    }, cancellationToken);
                }
                fee.Status = FeeStatus.Paid;
                fee.OrderId = order.Id;
                _fees.Update(fee);
                paidFees.Add(fee);
            }

            order.Status = OrderStatus.Paid;
            order.UpdatedAt = DateTime.UtcNow;
            _orders.Update(order);

            if (wasReleased)
            {
                _logger.LogWarning(
                    "Settlement: order {MerchantOrderId} re-opened from a released state and settled {Count} fee(s) on a late Paid.",
                    merchantOrderId, paidFees.Count);
            }

            foreach (var fee in paidFees)
            {
                await _outbox.EnqueueAsync(
                    FeePaidEvent.TypeKey,
                    new FeePaidFact(
                        fee.Id, order.Id, order.StudentId,
                        fee.SourceModule, fee.SourceReferenceId,
                        fee.TotalAmount, DateTime.UtcNow),
                    cancellationToken);
            }
        }
        else if (outcome is SettlementOutcome.Failed or SettlementOutcome.Expired && order.Status == OrderStatus.PendingPayment)
        {
            order.Status = outcome == SettlementOutcome.Expired ? OrderStatus.Expired : OrderStatus.Failed;
            order.UpdatedAt = DateTime.UtcNow;
            foreach (var fee in order.Fees)
            {
                // Keep fee.OrderId so a late Paid can re-open and settle this order
                // (D5). The fee is released for re-ordering via its status; if it is
                // selected into a new order, OrderService reassigns OrderId.
                fee.Status = FeeStatus.Pending;
                _fees.Update(fee);
            }
            _orders.Update(order);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent webhook/reconciliation updated the order or fees first
            // (RowVersion mismatch). The winner already settled — drop our staged
            // changes and treat this as an idempotent success rather than failing
            // the caller (no 500, no Hangfire poison, no duplicate payments).
            _db.ChangeTracker.Clear();
            _logger.LogInformation("Settlement: concurrent settle (RowVersion) for {MerchantOrderId}; treated as idempotent no-op.", merchantOrderId);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Unique Payment.FeeId / audit index rejected our duplicate — the
            // winner already created the payment(s). Drop staged changes and
            // treat as already-settled.
            _db.ChangeTracker.Clear();
            _logger.LogInformation("Settlement: concurrent settle (unique index) for {MerchantOrderId}; treated as idempotent no-op.", merchantOrderId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not Microsoft.Data.SqlClient.SqlException sql) return false;
        return sql.Number is 2627 or 2601;
    }
}
