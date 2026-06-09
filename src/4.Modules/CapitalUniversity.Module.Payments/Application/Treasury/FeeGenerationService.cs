using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Creates <see cref="StudentFee"/> rows from a service's Treasury receipt
/// mapping. <see cref="StudentFee.UnitAmount"/> and currency are snapshotted
/// from the receipt so later price changes never mutate an existing fee.
/// </summary>
public sealed class FeeGenerationService : IFeeGenerationService
{
    private readonly IServiceReceiptMappingRepository _mappings;
    private readonly ITreasuryReceiptRepository _receipts;
    private readonly IStudentFeeRepository _fees;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FeeGenerationService> _logger;

    public FeeGenerationService(
        IServiceReceiptMappingRepository mappings,
        ITreasuryReceiptRepository receipts,
        IStudentFeeRepository fees,
        IUnitOfWork unitOfWork,
        ILogger<FeeGenerationService> logger)
    {
        _mappings = mappings;
        _receipts = receipts;
        _fees = fees;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Guid?> GenerateFeeFromServiceAsync(
        Guid studentId,
        Guid studentServiceId,
        int quantity,
        string sourceModule,
        Guid? sourceReferenceId,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _mappings.GetActiveByServiceAsync(studentServiceId, cancellationToken);
        if (mapping is null)
        {
            _logger.LogInformation(
                "Fee generation: no active receipt mapping for service {ServiceId}; caller may use legacy path.",
                studentServiceId);
            return null;
        }

        var receipt = mapping.Receipt
            ?? await _receipts.GetByIdAsync(mapping.ReceiptId, cancellationToken)
            ?? throw new NotFoundException("Treasury receipt for the service mapping was not found.");

        // Fixed mappings always bill a single unit; quantity-based mappings honor
        // the caller's count (never below 1).
        var qty = mapping.QuantityMode == QuantityMode.Fixed ? 1 : Math.Max(1, quantity);

        var fee = new StudentFee
        {
            StudentId = studentId,
            ReceiptId = receipt.Id,
            Quantity = qty,
            UnitAmount = receipt.UnitAmount,
            TotalAmount = qty * receipt.UnitAmount,
            Currency = receipt.Currency,
            Status = FeeStatus.Pending,
            SourceModule = sourceModule,
            SourceReferenceId = sourceReferenceId,
        };

        await _fees.AddAsync(fee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Fee generation: created StudentFee {FeeId} ({Qty} x {Unit} {Currency}) for student {StudentId} from receipt {ReceiptId}.",
            fee.Id, qty, receipt.UnitAmount, receipt.Currency, studentId, receipt.Id);

        return fee.Id;
    }
}
