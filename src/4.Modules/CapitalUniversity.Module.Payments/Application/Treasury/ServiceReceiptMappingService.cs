using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Admin CRUD for service → receipt mappings. Enforces one active mapping per
/// service. Deactivation flips <c>IsActive</c> (soft), never deletes.
/// </summary>
public sealed class ServiceReceiptMappingService : IServiceReceiptMappingService
{
    private readonly IServiceReceiptMappingRepository _mappings;
    private readonly ITreasuryReceiptRepository _receipts;
    private readonly IUnitOfWork _unitOfWork;

    public ServiceReceiptMappingService(
        IServiceReceiptMappingRepository mappings,
        ITreasuryReceiptRepository receipts,
        IUnitOfWork unitOfWork)
    {
        _mappings = mappings;
        _receipts = receipts;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> CreateAsync(CreateServiceReceiptMappingRequest request, CancellationToken cancellationToken = default)
    {
        var receipt = await _receipts.GetByIdAsync(request.ReceiptId, cancellationToken)
            ?? throw new NotFoundException("Treasury receipt not found.");

        var existing = await _mappings.GetActiveByServiceAsync(request.StudentServiceId, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An active receipt mapping already exists for this service.");
        }

        var mapping = new ServiceReceiptMapping
        {
            StudentServiceId = request.StudentServiceId,
            ReceiptId = receipt.Id,
            QuantityMode = request.QuantityMode,
            IsActive = true,
        };

        await _mappings.AddAsync(mapping, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return mapping.Id;
    }

    public async Task<IReadOnlyList<ServiceReceiptMappingResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _mappings.GetAllAsync(cancellationToken);
        return all.Select(ToResponse).ToList();
    }

    public async Task<ServiceReceiptMappingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mapping = await _mappings.GetByIdAsync(id, cancellationToken);
        return mapping is null ? null : ToResponse(mapping);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mapping = await _mappings.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Service receipt mapping not found.");

        if (!mapping.IsActive) return;

        mapping.IsActive = false;
        mapping.UpdatedAt = DateTime.UtcNow;
        _mappings.Update(mapping);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ServiceReceiptMappingResponse ToResponse(ServiceReceiptMapping m) => new()
    {
        Id = m.Id,
        StudentServiceId = m.StudentServiceId,
        ReceiptId = m.ReceiptId,
        ReceiptName = m.Receipt?.Name ?? string.Empty,
        UnitAmount = m.Receipt?.UnitAmount ?? 0m,
        Currency = m.Receipt?.Currency ?? "EGP",
        QuantityMode = m.QuantityMode,
        IsActive = m.IsActive,
    };
}
