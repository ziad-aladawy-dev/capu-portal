namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Generates a <c>StudentFee</c> from a service's Treasury receipt mapping.
/// Price and currency are snapshotted from the receipt at generation time.
/// </summary>
public interface IFeeGenerationService
{
    /// <summary>
    /// Resolves the active <c>ServiceReceiptMapping</c> for the service, reads
    /// the linked Treasury receipt, and creates a <c>StudentFee</c> of
    /// <c>Quantity × UnitAmount</c>. Returns the new fee id, or <c>null</c> when
    /// the service has no active mapping — letting the caller fall back to a
    /// legacy fee path without changing existing behavior.
    /// </summary>
    Task<Guid?> GenerateFeeFromServiceAsync(
        Guid studentId,
        Guid studentServiceId,
        int quantity,
        string sourceModule,
        Guid? sourceReferenceId,
        CancellationToken cancellationToken = default);
}
