using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Payments;
using CapitalUniversity.Core.Abstractions.Payments.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Payments;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Payments;

/// <summary>
/// Owns invoice lifecycle (create / read / cancel). The catalog of fees
/// authored by other modules lands on this entity only — Payments never
/// interprets a <c>SourceModule</c> or <c>ReferenceId</c>. Cache strategy:
/// <c>invoice:object:{id}</c> shared-object payload per
/// <c>docs/caching-strategy.md</c>, invalidated on every mutation.
/// </summary>
public class InvoiceService : IInvoiceService
{
    internal const string CacheKeyPrefix = "invoice:object:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IInvoiceRepository _invoices;
    private readonly IValidator<CreateInvoiceRequest> _createValidator;
    private readonly ICacheService _cache;

    public InvoiceService(
        IUnitOfWork unitOfWork,
        IInvoiceRepository invoices,
        IValidator<CreateInvoiceRequest> createValidator,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _invoices = invoices;
        _createValidator = createValidator;
        _cache = cache;
    }

    public async Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);
        var cached = await _cache.GetAsync<InvoiceResponse>(key, cancellationToken);
        if (cached is not null) return cached;

        var invoice = await _invoices.GetByIdAsync(id, includeItems: true, cancellationToken: cancellationToken);
        if (invoice is null) return null;

        var dto = ToResponse(invoice);
        await _cache.SetAsync(key, dto, CacheTtl, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var invoices = await _invoices.GetForStudentAsync(studentId, cancellationToken);
        // List view — slim summary, no items. Callers fetch by ID for full payload.
        return invoices.Select(i => new InvoiceResponse
        {
            Id = i.Id,
            StudentId = i.StudentId,
            Status = i.Status,
            TotalAmount = i.TotalAmount,
            Currency = i.Currency,
            DueAt = i.DueAt,
            CreatedAt = i.CreatedAt,
        }).ToList();
    }

    public async Task<Guid> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var invoice = new Invoice
        {
            StudentId = request.StudentId,
            Currency = request.Currency,
            DueAt = request.DueAt,
            Status = InvoiceStatus.Pending,
        };

        foreach (var i in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Amount = i.Amount,
                FeeType = i.FeeType,
                SourceModule = i.SourceModule,
                ReferenceId = i.ReferenceId,
                Description = i.Description,
            });
        }
        invoice.TotalAmount = invoice.Items.Sum(it => it.Amount);

        await _invoices.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }

    public async Task CancelAsync(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoices.GetByIdAsync(id, includeItems: false, includeTransactions: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ConflictException("Paid invoices cannot be cancelled — issue a refund instead.");
        }
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return;
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    internal static InvoiceResponse ToResponse(Invoice invoice) => new()
    {
        Id = invoice.Id,
        StudentId = invoice.StudentId,
        Status = invoice.Status,
        TotalAmount = invoice.TotalAmount,
        Currency = invoice.Currency,
        DueAt = invoice.DueAt,
        CreatedAt = invoice.CreatedAt,
        Items = invoice.Items.Select(i => new InvoiceItemResponse
        {
            Id = i.Id,
            Amount = i.Amount,
            FeeType = i.FeeType,
            SourceModule = i.SourceModule,
            ReferenceId = i.ReferenceId,
            Description = i.Description,
        }).ToList(),
    };
}
