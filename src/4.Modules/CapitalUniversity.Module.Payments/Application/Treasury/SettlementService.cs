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
/// dedups repeated identical outcomes. <see cref="IOutbox"/> is optional so the
/// service also resolves in hosts that do not wire the outbox (reconciliation
/// host); the webhook path always has it and drives FeePaidEvent delivery.
/// </summary>
public sealed class SettlementService : ISettlementService
{
    private readonly IOrderRepository _orders;
    private readonly IStudentFeeRepository _fees;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentTransactionRepository _transactions;
    private readonly CoreDbContext _db;
    private readonly ILogger<SettlementService> _logger;
    private readonly IOutbox? _outbox;

    public SettlementService(
        IOrderRepository orders,
        IStudentFeeRepository fees,
        IPaymentRepository payments,
        IPaymentTransactionRepository transactions,
        CoreDbContext db,
        ILogger<SettlementService> logger,
        IOutbox? outbox = null)
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
        CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByMerchantOrderIdAsync(merchantOrderId, includeFees: true, cancellationToken: cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Settlement: no order for MerchantOrderId {MerchantOrderId}; ignoring.", merchantOrderId);
            return;
        }

        var idempotencyKey = $"{source}:{outcome}";
        if (await _transactions.ExistsAsync(merchantOrderId, idempotencyKey, cancellationToken))
        {
            // This exact outcome was already processed for this order.
            return;
        }

        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = merchantOrderId,
            Gateway = order.Gateway,
            Type = source,
            Status = outcome switch
            {
                SettlementOutcome.Paid => GatewayTransactionStatus.Succeeded,
                SettlementOutcome.Failed or SettlementOutcome.Expired => GatewayTransactionStatus.Failed,
                _ => GatewayTransactionStatus.Pending,
            },
            Amount = order.TotalAmount,
            RawResponse = string.IsNullOrEmpty(rawPayload) ? "{}" : rawPayload,
            IdempotencyKey = idempotencyKey,
        }, cancellationToken);

        var paidFees = new List<StudentFee>();

        if (outcome == SettlementOutcome.Paid && order.Status != OrderStatus.PendingPayment)
        {
            // A Paid notification is only valid for an order awaiting payment.
            // Anything else (already Paid, Failed, Expired, Cancelled, Refunded)
            // is treated as an idempotent no-op — never create payments for an
            // order whose fees may have been released or re-ordered.
            _logger.LogWarning(
                "Settlement: Paid notification for order {MerchantOrderId} in non-PendingPayment state {Status}; ignored (idempotent).",
                merchantOrderId, order.Status);
        }
        else if (outcome == SettlementOutcome.Paid)
        {
            foreach (var fee in order.Fees)
            {
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

            if (_outbox is not null)
            {
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
        }
        else if (outcome is SettlementOutcome.Failed or SettlementOutcome.Expired && order.Status == OrderStatus.PendingPayment)
        {
            order.Status = outcome == SettlementOutcome.Expired ? OrderStatus.Expired : OrderStatus.Failed;
            order.UpdatedAt = DateTime.UtcNow;
            foreach (var fee in order.Fees)
            {
                fee.Status = FeeStatus.Pending;
                fee.OrderId = null;
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
