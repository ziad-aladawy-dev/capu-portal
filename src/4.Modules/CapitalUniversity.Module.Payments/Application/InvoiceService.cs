using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
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
    // Collection caches (per-student list + search pages) keyed by a version
    // stamp; any invoice mutation — including a payment recorded by
    // PaymentVerificationService or fees appended by FeeCreationService — rotates
    // it and orphans them all in one write.
    internal const string CollectionVersionKey = "invoice:coll:ver";
    internal const string StudentListKeyPrefix = "invoice:student:";
    internal const string SearchKeyPrefix = "invoice:search:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan CollectionVersionTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Rotate the invoice collection version. Static so sibling services
    /// (PaymentVerificationService, FeeCreationService) that mutate an invoice's
    /// read model can invalidate the list/search caches consistently.
    /// </summary>
    internal static Task BumpCollectionVersionAsync(ICacheService cache, CancellationToken cancellationToken) =>
        cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), CollectionVersionTtl, cancellationToken);

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
        // Stampede-protected shared-object read-through; scope enforced per read.
        var dto = await _cache.GetOrSetAsync<InvoiceResponse>(
            key,
            async ct =>
            {
                var invoice = await _invoices.GetByIdAsync(id, includeItems: true, cancellationToken: ct);
                return invoice is null ? null : ToResponse(invoice);
            },
            CacheTtl,
            cancellationToken);

        if (dto is null) return null;

        return await _scope.CanAccessStudentAsync(dto.StudentId, cancellationToken)
            ? Localize(dto)
            : null;
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

        // List view — slim summary, no items (no localizable content), so it is
        // cached scope-neutral per student under the collection version, behind
        // the per-caller scope gate above. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<InvoiceResponse>>(
            $"{StudentListKeyPrefix}{studentId:N}:{version}",
            async ct => (await _invoices.GetForStudentAsync(studentId, ct)).Select(ToSummary).ToList(),
            CacheTtl,
            cancellationToken);

        return new List<InvoiceResponse>(cached ?? new List<InvoiceResponse>());
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
        // A new invoice must surface in the student's list / search reads.
        await BumpCollectionVersionAsync(_cache, cancellationToken);
        return invoice.Id;
    }

    public async Task CancelAsync(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadForCancelAsync(id, cancellationToken);
        if (!ApplyCancel(invoice)) return;

        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(_cache, cancellationToken);
    }

    /// <summary>
    /// Fetch a tracked invoice by id and require the caller's scope to cover
    /// its student. Both miss and out-of-scope map to the same NotFound key so
    /// the caller cannot distinguish (P1.1). Loads the transaction collection
    /// to match the original single-row cancel load.
    /// </summary>
    private async Task<Invoice> LoadForCancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetByIdAsync(id, includeItems: false, includeTransactions: true, cancellationToken: cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);

        if (!await _scope.CanAccessStudentAsync(invoice.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Payments.InvoiceNotFound);
        }
        return invoice;
    }

    /// <summary>
    /// Apply the cancel transition to a loaded, in-scope invoice. Returns
    /// <c>false</c> when the invoice is already cancelled (idempotent no-op,
    /// no persist needed) and <c>true</c> when the status was flipped. Throws
    /// <see cref="ConflictException"/> for a paid invoice and
    /// <see cref="InvalidOperationException"/> (via <c>EnsureMutable</c>) for a
    /// closed one — identical to the original single-row cancel contract.
    /// </summary>
    private static bool ApplyCancel(Invoice invoice)
    {
        invoice.EnsureMutable();

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new ConflictException(LocalizedKeys.Payments.PaidCannotCancel);
        }
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return false;
        }

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadForWriteAsync(id, cancellationToken);
        invoice.Close();
        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(_cache, cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadForWriteAsync(id, cancellationToken);
        invoice.Reopen();
        _invoices.Update(invoice);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(_cache, cancellationToken);
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

    public async Task<PagedResult<InvoiceResponse>> SearchAsync(InvoiceSearchQuery query, CancellationToken cancellationToken = default)
    {
        // P1.1 — if the caller pinned a studentId, enforce scope on that one id
        // (returns an empty page if the student is invisible to the caller — no
        // existence leak). Cross-student queries are guarded at the route
        // permission level (Invoices.View — admin-scoped).
        if (query.StudentId.HasValue &&
            !await _scope.CanAccessStudentAsync(query.StudentId.Value, cancellationToken))
        {
            return new PagedResult<InvoiceResponse>
            {
                Items = new List<InvoiceResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0,
                TotalPages = 0,
            };
        }

        // Slim summary projection (drops Items) cached under the collection
        // version + query signature. Stampede-protected. The pinned-student scope
        // gate above runs before any cache read; cross-student queries are route
        // permission-gated, so the page itself is scope-neutral.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var page = await _cache.GetOrSetAsync<PagedResult<InvoiceResponse>>(
            $"{SearchKeyPrefix}{version}:{BuildSearchSignature(query)}",
            async ct =>
            {
                var result = await _invoices.SearchAsync(query, ct);
                return new PagedResult<InvoiceResponse>
                {
                    Items = result.Items.Select(ToSummary).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? new PagedResult<InvoiceResponse>
            {
                Items = new List<InvoiceResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
            };

        return new PagedResult<InvoiceResponse>
        {
            Items = new List<InvoiceResponse>(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    /// <summary>
    /// Per-row driver mirroring <c>CourseOfferingService.BulkApplyAsync</c>:
    /// loads each invoice through <see cref="LoadForCancelAsync"/> (re-using the
    /// scope guard), applies the cancel transition, and commits independently so
    /// a failed peer does not roll back successes ("partial success" model).
    /// Reason is not persisted today — captured only on the audit/log path,
    /// matching the prior behaviour.
    /// </summary>
    public async Task<BulkActionResult> BulkCancelAsync(IReadOnlyList<Guid> ids, string reason, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(ids.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in ids.Distinct())
        {
            try
            {
                var invoice = await LoadForCancelAsync(id, cancellationToken);
                if (ApplyCancel(invoice))
                {
                    _invoices.Update(invoice);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(_cache, cancellationToken);
                }
                succeeded.Add(id);
            }
            catch (NotFoundException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.NotFound, Message = ex.Message });
            }
            catch (ConflictException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.Conflict, Message = ex.Message });
            }
        }

        return BulkActionResult.From(succeeded, failures);
    }

    /// <summary>Slim list/search summary — drops Items (no localizable content).</summary>
    private static InvoiceResponse ToSummary(Invoice i) => new()
    {
        Id = i.Id,
        StudentId = i.StudentId,
        Status = i.Status,
        TotalAmount = i.TotalAmount,
        Currency = i.Currency,
        DueAt = i.DueAt,
        CreatedAt = i.CreatedAt,
    };

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", CollectionVersionTtl, cancellationToken);
        return "0";
    }

    private static string BuildSearchSignature(InvoiceSearchQuery q) =>
        string.Join('|',
            $"stu={q.StudentId?.ToString("N") ?? string.Empty}",
            $"status={(q.Status.HasValue ? ((int)q.Status.Value).ToString() : string.Empty)}",
            $"ifrom={q.IssuedFrom?.ToString("O") ?? string.Empty}",
            $"ito={q.IssuedTo?.ToString("O") ?? string.Empty}",
            $"dfrom={q.DueFrom?.ToString("O") ?? string.Empty}",
            $"dto={q.DueTo?.ToString("O") ?? string.Empty}",
            $"min={q.MinAmount?.ToString() ?? string.Empty}",
            $"max={q.MaxAmount?.ToString() ?? string.Empty}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

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
