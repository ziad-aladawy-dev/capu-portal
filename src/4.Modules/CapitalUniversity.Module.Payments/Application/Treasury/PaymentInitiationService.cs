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
/// Drives Created → PendingPayment via the gateway-specific Treasury initiate.
/// Builds the receipt-id list expanded per fee quantity, sources billing details
/// from the student profile, persists an initiation intent before the external
/// call (so a crash is recoverable), then stores the MerchantOrderId + redirect.
/// </summary>
public sealed class PaymentInitiationService : IPaymentInitiationService
{
    private readonly IOrderRepository _orders;
    private readonly ITreasuryReceiptRepository _receipts;
    private readonly IPaymentTransactionRepository _transactions;
    private readonly ITreasuryClient _treasury;
    private readonly IStudentRepository _students;
    private readonly IEffectiveScope _scope;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TreasuryOptions _options;

    public PaymentInitiationService(
        IOrderRepository orders,
        ITreasuryReceiptRepository receipts,
        IPaymentTransactionRepository transactions,
        ITreasuryClient treasury,
        IStudentRepository students,
        IEffectiveScope scope,
        IUnitOfWork unitOfWork,
        IOptions<TreasuryOptions> options)
    {
        _orders = orders;
        _receipts = receipts;
        _transactions = transactions;
        _treasury = treasury;
        _students = students;
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

        // Idempotent re-initiation.
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

        // D7 — resolve every receipt; fail initiation if any is unresolved. The
        // id list is expanded per quantity (a quantity-N fee adds its id N times).
        var receiptIds = new List<int>();
        foreach (var fee in order.Fees)
        {
            var receipt = await _receipts.GetByIdAsync(fee.ReceiptId, cancellationToken)
                ?? throw new ConflictException($"Treasury receipt for fee {fee.Id} could not be resolved; initiation aborted.");
            if (!int.TryParse(receipt.ExternalReceiptId, out var externalId))
            {
                throw new ConflictException($"Treasury receipt {receipt.Id} has a non-numeric external id; initiation aborted.");
            }
            for (var i = 0; i < Math.Max(1, fee.Quantity); i++)
            {
                receiptIds.Add(externalId);
            }
        }

        // Billing details from the student profile (required by BM + eFinance).
        var student = await _students.GetByIdAsync(order.StudentId)
            ?? throw new NotFoundException("Student not found for order.");
        var (firstName, lastName) = SplitName(student.Name);

        var request = new TreasuryInitiateRequest
        {
            ReceiptIds = receiptIds,
            StudentReferenceId = student.NationalId,
            RedirectUrl = returnUrl,
            Currency = order.Currency,
            Billing = new TreasuryBillingDetails
            {
                FirstName = firstName,
                LastName = lastName,
                EmailAddress = student.Email,
                MobileNumber = student.PhoneNumber,
                Currency = order.Currency,
            },
        };

        // D4 — persist an initiation intent BEFORE the external call so a crash
        // between the gateway session creation and our commit is recoverable.
        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = string.Empty,
            Gateway = order.Gateway,
            Type = TransactionType.Initiate,
            Status = GatewayTransactionStatus.Pending,
            Amount = order.TotalAmount,
            IdempotencyKey = $"initiate-intent:{Guid.NewGuid():N}",
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        TreasuryInitiateResult result;
        try
        {
            result = await _treasury.InitiateAsync(order.Gateway, request, cancellationToken);
        }
        catch
        {
            await RecordFailedAsync(order, cancellationToken);
            throw;
        }

        if (string.IsNullOrEmpty(result.MerchantOrderId))
        {
            await RecordFailedAsync(order, cancellationToken);
            throw new InvalidOperationException("Treasury did not return a MerchantOrderId for the order.");
        }

        order.MerchantOrderId = result.MerchantOrderId;
        order.RedirectUrl = result.RedirectUrl;
        order.Status = OrderStatus.PendingPayment;
        order.ExpiresAt = DateTime.UtcNow.AddMinutes(_options.OrderTtlMinutes);
        order.UpdatedAt = DateTime.UtcNow;
        _orders.Update(order);

        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = result.MerchantOrderId,
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

    private async Task RecordFailedAsync(Order order, CancellationToken cancellationToken)
    {
        await _transactions.AddAsync(new PaymentTransaction
        {
            OrderId = order.Id,
            MerchantOrderId = string.Empty,
            Gateway = order.Gateway,
            Type = TransactionType.Initiate,
            Status = GatewayTransactionStatus.Failed,
            Amount = order.TotalAmount,
            IdempotencyKey = $"initiate-failed:{Guid.NewGuid():N}",
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (string First, string Last) SplitName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (string.Empty, string.Empty);
        var trimmed = name.Trim();
        var idx = trimmed.IndexOf(' ');
        return idx < 0 ? (trimmed, string.Empty) : (trimmed[..idx], trimmed[(idx + 1)..].Trim());
    }
}
