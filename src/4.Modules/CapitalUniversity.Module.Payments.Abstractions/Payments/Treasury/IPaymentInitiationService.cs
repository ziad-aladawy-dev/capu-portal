using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Initiates payment for a <c>Created</c> order via HU Treasury, persisting the
/// returned MerchantOrderId and moving the order to PendingPayment.
/// </summary>
public interface IPaymentInitiationService
{
    /// <summary>
    /// Initiates (or, if already PendingPayment, returns the existing session for)
    /// the order. <paramref name="redirectUrl"/> overrides the configured default.
    /// </summary>
    Task<OrderInitiationResponse> InitiateAsync(Guid orderId, string? redirectUrl = null, CancellationToken cancellationToken = default);
}
