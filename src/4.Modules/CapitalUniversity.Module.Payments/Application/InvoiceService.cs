using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Domain;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;
using CapitalUniversity.Modules.Payments.Repositories;

namespace CapitalUniversity.Modules.Payments.Application;

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
    private readonly IEffectiveScope _scope;
    private readonly ILocalizationService _localization;

    public InvoiceService(
        IUnitOfWork unitOfWork,
        IInvoiceRepository invoices,
        IValidator<CreateInvoiceRequest> createValidator,
        ICacheService cache,
        IEffectiveScope scope,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _invoices = invoices;
        _createValidator = createValidator;
        _cache = cache;
        _scope = scope;
        _localization = localization;
    }

    public async Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);

        // P1.1 — cache stays shared, but scope is enforced on every read. The
        // cached projection carries StudentId, so we check before returning.
        // Out-of-scope returns null so the controller maps to 404 (no
        // existence leak).
        // Cache stores the culture-neutral payload (line Descriptions still in
        // {"ar":"…","en":"…"} JSON shape). Decoding runs on return so two
        // requests with different Accept-Language share the same cache entry.
        var cached = await _cache.GetAsync<InvoiceResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return await _scope.CanAccessStudentAsync(cached.StudentId, cancellationToken)
                ? Localize(cached)
                : null;
        }

        var invoice = await _invoices.GetByIdAsync(id, includeItems: true, cancellationToken: cancellationToken);
        if (invoice is null) return null;

        if (!await _scope.CanAccessStudentAsync(invoice.StudentId, cancellationToken)) return null;

        var dto = ToResponse(invoice);
        await _cache.SetAsync(key, dto, CacheTtl, cancellationToken);
        return Localize(dto);
    }

    /// <summary>
    /// Decode the bilingual <c>Description</c> on each invoice item against
    /// the current culture. The invoice's identity fields (StudentId, Status,
    /// Currency, Amount) are not localizable.
    /// </summary>
    private InvoiceResponse Localize(InvoiceResponse response)
    {
        if (response.Items is { Count: > 0 })
        {
            foreach (var item in response.Items)
            {
                item.Description = _localization.Get<string>(item.Description);
            }
        }
        return response;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        // Caller asserts a student id; refuse silently if out-of-scope.
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<InvoiceResponse>();
        }

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

        // P1.1 — creating an invoice for a student outside scope is treated as
        // not-found, not forbidden, to avoid telling the caller whether the
        // student exists.
        if (!await _scope.CanAccessStudentAsync(request.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Payments.StudentNotFound);
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
                Description = LocalizedJson.Normalize(i.Description),
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
            ?? throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);

        // Out-of-scope is reported as not-found — caller cannot distinguish.
        if (!await _scope.CanAccessStudentAsync(invoice.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);
        }

        invoice.EnsureMutable();

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ConflictException(LocalizedKeys.Payments.PaidCannotCancel);
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

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadForWriteAsync(id, cancellationToken);
        invoice.Close();
        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadForWriteAsync(id, cancellationToken);
        invoice.Reopen();
        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    private async Task<Invoice> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetByIdAsync(id, includeItems: false, includeTransactions: false, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);

        if (!await _scope.CanAccessStudentAsync(invoice.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);
        }
        return invoice;
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
