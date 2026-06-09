using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Assembles orders from selected unpaid fees and manages their pre-payment
/// lifecycle. Initiation/settlement live in later phases.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Creates an <c>Order(Created)</c> from the selected fees. All fees must be
    /// Pending, owned by the student, and share one currency. Selected fees move
    /// to IncludedInOrder. One-active-order-per-fee is enforced under optimistic
    /// concurrency.
    /// </summary>
    Task<OrderResponse> CreateOrderAsync(Guid studentId, IReadOnlyList<Guid> feeIds, Gateway gateway, CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a <c>Created</c> order and releases its fees back to Pending.</summary>
    Task CancelAsync(Guid orderId, CancellationToken cancellationToken = default);
}
