using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Assembles orders from selected unpaid fees. The status flip on fees runs
/// under <see cref="ConcurrencyRetry"/> + <c>StudentFee.RowVersion</c> so two
/// concurrent orders can never both claim the same fee.
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IStudentFeeRepository _fees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEffectiveScope _scope;

    public OrderService(
        IOrderRepository orders,
        IStudentFeeRepository fees,
        IUnitOfWork unitOfWork,
        IEffectiveScope scope)
    {
        _orders = orders;
        _fees = fees;
        _unitOfWork = unitOfWork;
        _scope = scope;
    }

    public async Task<OrderResponse> CreateOrderAsync(Guid studentId, IReadOnlyList<Guid> feeIds, Gateway gateway, CancellationToken cancellationToken = default)
    {
        if (feeIds is null || feeIds.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["feeIds"] = new[] { "At least one fee must be selected." },
            });
        }

        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            throw new NotFoundException("Student not found.");
        }

        var distinct = feeIds.Distinct().ToList();
        var attempt = 0;

        return await ConcurrencyRetry.ExecuteAsync(async ct =>
        {
            if (attempt++ > 0)
            {
                // Drop stale tracked entities so the reload sees current RowVersions.
                _fees.ResetChangeTracker();
            }

            var fees = await _fees.GetByIdsAsync(distinct, ct);
            if (fees.Count != distinct.Count)
            {
                throw new NotFoundException("One or more selected fees were not found.");
            }
            if (fees.Any(f => f.StudentId != studentId))
            {
                throw new NotFoundException("One or more selected fees do not belong to the student.");
            }
            if (fees.Any(f => f.Status != FeeStatus.Pending))
            {
                throw new ConflictException("One or more selected fees are not available for ordering (must be Pending).");
            }

            var currencies = fees.Select(f => f.Currency).Distinct().ToList();
            if (currencies.Count > 1)
            {
                throw new ConflictException("Selected fees have mixed currencies.");
            }

            var order = new Order
            {
                StudentId = studentId,
                Gateway = gateway,
                Status = OrderStatus.Created,
                Currency = currencies[0],
                TotalAmount = fees.Sum(f => f.TotalAmount),
            };
            await _orders.AddAsync(order, ct);

            foreach (var fee in fees)
            {
                fee.Status = FeeStatus.IncludedInOrder;
                fee.OrderId = order.Id;
                _fees.Update(fee);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ToResponse(order, fees);
        }, cancellationToken: cancellationToken);
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, includeFees: true, cancellationToken: cancellationToken);
        if (order is null) return null;
        if (!await _scope.CanAccessStudentAsync(order.StudentId, cancellationToken)) return null;
        return ToResponse(order, order.Fees);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<OrderResponse>();
        }
        var orders = await _orders.GetForStudentAsync(studentId, cancellationToken);
        return orders.Select(o => ToResponse(o, Array.Empty<StudentFee>())).ToList();
    }

    public async Task CancelAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, includeFees: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        if (!await _scope.CanAccessStudentAsync(order.StudentId, cancellationToken))
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.Status != OrderStatus.Created)
        {
            throw new ConflictException("Only a Created order can be cancelled.");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        foreach (var fee in order.Fees)
        {
            fee.Status = FeeStatus.Pending;
            fee.OrderId = null;
            _fees.Update(fee);
        }
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static OrderResponse ToResponse(Order o, IEnumerable<StudentFee> fees) => new()
    {
        Id = o.Id,
        StudentId = o.StudentId,
        Status = o.Status,
        Gateway = o.Gateway,
        MerchantOrderId = o.MerchantOrderId,
        RedirectUrl = o.RedirectUrl,
        TotalAmount = o.TotalAmount,
        Currency = o.Currency,
        CreatedAt = o.CreatedAt,
        Fees = fees.Select(ToFeeResponse).ToList(),
    };

    private static StudentFeeResponse ToFeeResponse(StudentFee f) => new()
    {
        Id = f.Id,
        StudentId = f.StudentId,
        ReceiptId = f.ReceiptId,
        Quantity = f.Quantity,
        UnitAmount = f.UnitAmount,
        TotalAmount = f.TotalAmount,
        Currency = f.Currency,
        Status = f.Status,
        SourceModule = f.SourceModule,
        SourceReferenceId = f.SourceReferenceId,
        OrderId = f.OrderId,
        CreatedAt = f.CreatedAt,
    };
}
