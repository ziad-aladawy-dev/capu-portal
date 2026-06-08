using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Drives the Created → PendingPayment transition: calls the gateway initiate
/// endpoint, persists the MerchantOrderId + redirect + session, and records an
/// Initiate audit transaction. Re-initiation of an already-PendingPayment order
/// is idempotent (returns the stored session).
/// </summary>
public sealed class PaymentInitiationService : IPaymentInitiationService
{
    private readonly IOrderRepository _orders;
    private readonly ITreasuryReceiptRepository _receipts;
    private readonly IPaymentTransactionRepository _transactions;
    private readonly ITreasuryClient _treasury;
    private readonly IEffectiveScope _scope;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TreasuryOptions _options;

    public PaymentInitiationService(
        IOrderRepository orders,
        ITreasuryReceiptRepository receipts,
        IPaymentTransactionRepository transactions,
        ITreasuryClient treasury,
        IEffectiveScope scope,
        IUnitOfWork unitOfWork,
        IOptions<TreasuryOptions> options)
    {
        _orders = orders;
        _receipts = receipts;
        _transactions = transactions;
        _treasury = treasury;
        _scope = scope;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<OrderInitiationResponse> InitiateAsync(Guid orderId, string? redirectUrl = null, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, includeFees: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        if (!await _scope.CanAccessStudentAsync(order.StudentId, cancellationToken))
        {
            throw new NotFoundException("Order not found.");
        }

        // Idempotent re-initiation: already has a session.
        if (order.Status == OrderStatus.PendingPayment && !string.IsNullOrEmpty(order.MerchantOrderId))
        {
            return new OrderInitiationResponse
            {
                OrderId = order.Id,
                MerchantOrderId = order.MerchantOrderId,
                RedirectUrl = order.RedirectUrl,
            };
        }

        if (order.Status != OrderStatus.Created)
        {
            throw new ConflictException("Only a Created order can be initiated.");
        }

        var returnUrl = string.IsNullOrWhiteSpace(redirectUrl) ? _options.RedirectUrl : redirectUrl;

        // Resolve external (Treasury) receipt ids for the order's fees.
        var receiptIds = new List<string>(order.Fees.Count);
        foreach (var fee in order.Fees)
        {
            var receipt = await _receipts.GetByIdAsync(fee.ReceiptId, cancellationToken);
            if (receipt is not null && !string.IsNullOrEmpty(receipt.ExternalReceiptId))
            {
                receiptIds.Add(receipt.ExternalReceiptId);
            }
        }

        var request = new TreasuryInitiateRequest
        {
            ReceiptIds = receiptIds,
            StudentReferenceId = order.StudentId.ToString(),
            RedirectUrl = returnUrl,
            Amount = order.TotalAmount,
            Currency = order.Currency,
        };

        TreasuryInitiateResponse resp;
        try
        {
            resp = await _treasury.InitiateAsync(order.Gateway, request, cancellationToken);
        }
        catch
        {
            // Record the failed attempt for audit, leaving the order Created.
            await _transactions.AddAsync(new PaymentTransaction
            {
                OrderId = order.Id,
                MerchantOrderId = string.Empty,
                Gateway = order.Gateway,
                Type = TransactionType.Initiate,
                Status = GatewayTransactionStatus.Failed,
                Amount = order.TotalAmount,
                IdempotencyKey = $"initiate-failed:{order.Id:N}",
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        order.MerchantOrderId = resp.MerchantOrderId;
        order.RedirectUrl = resp.RedirectUrl;
        order.GatewaySessionRef = resp.SessionReference ?? string.Empty;
        order.Status = OrderStatus.PendingPayment;
        order.ExpiresAt = DateTime.UtcNow.AddMinutes(_options.OrderTtlMinutes);
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);

        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = resp.MerchantOrderId,
            Gateway = order.Gateway,
            Type = TransactionType.Initiate,
            Status = GatewayTransactionStatus.Succeeded,
            Amount = order.TotalAmount,
            IdempotencyKey = "initiate",
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrderInitiationResponse
        {
            OrderId = order.Id,
            MerchantOrderId = order.MerchantOrderId,
            RedirectUrl = order.RedirectUrl,
        };
    }
}
