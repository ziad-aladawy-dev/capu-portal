using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Application.Courses.Mappings;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Courses;

/// <summary>
/// Catalog-level course service. Cache strategy follows
/// <c>docs/caching-strategy.md</c>: only the shared object payload is cached
/// under <c>course:object:{id}</c>; visibility lists belong to higher layers
/// (the catalog is school-wide so there is no per-user filter to apply here).
///
/// <para>
/// Mutations invalidate just the object key; lookups are cache-aside.
/// </para>
/// </summary>
public class CourseService : ICourseService
{
    internal const string CacheKeyPrefix = "course:object:";
    // Collection caches (active list + search pages) are keyed by a catalog
    // version stamp; any mutation rotates the stamp and orphans them all in one
    // write — the same epoch/version invalidation model the permission cache uses.
    internal const string CatalogVersionKey = "course:catalog:ver";
    internal const string ActiveListKeyPrefix = "course:list:active:";
    internal const string SearchKeyPrefix = "course:search:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    // The stamp only changes on mutation, so it long-outlives the data TTL.
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourseRepository _courses;
    private readonly IValidator<CreateCourseRequest> _createValidator;
    private readonly IValidator<(Guid Id, UpdateCourseRequest Request)> _updateValidator;
    private readonly ICacheService _cache;
    private readonly ILocalizationService _localization;
    private readonly CourseMapper _mapper = new();

    public CourseService(
        IUnitOfWork unitOfWork,
        ICourseRepository courses,
        IValidator<CreateCourseRequest> createValidator,
        IValidator<(Guid Id, UpdateCourseRequest Request)> updateValidator,
        ICacheService cache,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _courses = courses;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _cache = cache;
        _localization = localization;
    }

    public async Task<CourseResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Cache stores the culture-neutral response (Title still in
        // {"ar":"…","en":"…"} shape). Decoding happens on the way out so two
        // requests with different Accept-Language hit the same cache entry
        // without poisoning each other.
        //
        // GetOrSetAsync is the stampede-protected read-through: on a cache miss
        // under load, only one instance runs the DB lookup and the rest reuse
        // its result (see StampedeProtectedCacheService). A null course is not
        // cached, so a missing id still returns null on every call.
        var response = await _cache.GetOrSetAsync<CourseResponse>(
            CacheKey(id),
            async ct =>
            {
                var course = await _courses.GetByIdAsync(id, ct);
                return course is null ? null : _mapper.MapToResponse(course);
            },
            CacheTtl,
            cancellationToken);

        return response is null ? null : Localize(response);
    }

    public async Task<IReadOnlyList<CourseResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        // Stampede-protected: the active catalog is read on every catalog/login
        // view, so a cold-cache burst must run one DB scan, not one per request
        // per instance. Cached payload is culture-neutral; localization happens
        // on the way out against fresh copies (no cross-culture poisoning).
        var version = await GetCatalogVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<CourseResponse>>(
            $"{ActiveListKeyPrefix}{version}",
            async ct => (await _courses.GetActiveAsync(ct)).Select(_mapper.MapToResponse).ToList(),
            CacheTtl,
            cancellationToken);

        return (cached ?? new List<CourseResponse>()).Select(LocalizeCopy).ToList();
    }

    public async Task<PagedResult<CourseResponse>> SearchAsync(CourseSearchQuery query, CancellationToken cancellationToken = default)
    {
        // The catalog is school-wide (no per-user filter), so a given query+page
        // yields the same result for everyone — safe to cache by query signature
        // under the catalog version. Stampede-protected read-through.
        var version = await GetCatalogVersionAsync(cancellationToken);
        var cacheKey = $"{SearchKeyPrefix}{version}:{BuildSearchSignature(query)}";

        var page = await _cache.GetOrSetAsync<PagedResult<CourseResponse>>(
            cacheKey,
            async ct =>
            {
                var result = await _courses.SearchAsync(query, ct);
                return new PagedResult<CourseResponse>
                {
                    Items = result.Items.Select(_mapper.MapToResponse).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                };
            },
            CacheTtl,
            cancellationToken);

        if (page is null)
        {
            return new PagedResult<CourseResponse>
            {
                Items = new List<CourseResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
            };
        }

        return new PagedResult<CourseResponse>
        {
            Items = page.Items.Select(LocalizeCopy).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    private async Task<string> GetCatalogVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CatalogVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CatalogVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCatalogVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CatalogVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    private static string BuildSearchSignature(CourseSearchQuery q) =>
        string.Join('|',
            $"cat={(q.Category.HasValue ? ((int)q.Category.Value).ToString() : string.Empty)}",
            $"active={q.IsActive?.ToString() ?? string.Empty}",
            $"min={q.MinCreditHours?.ToString() ?? string.Empty}",
            $"max={q.MaxCreditHours?.ToString() ?? string.Empty}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

    /// <summary>
    /// Localize onto a NEW response so list/search cache entries (which may be the
    /// same instance under the in-memory provider) are never mutated in place.
    /// </summary>
    private CourseResponse LocalizeCopy(CourseResponse source) => new()
    {
        Id = source.Id,
        Code = _localization.Get<string>(source.Code),
        Title = _localization.Get<string>(source.Title),
        CreditHours = source.CreditHours,
        Category = source.Category,
        IsActive = source.IsActive,
        IsClosed = source.IsClosed,
    };

    /// <summary>
    /// Decode the bilingual <c>Title</c> field on a <see cref="CourseResponse"/>
    /// against the current culture. <c>Code</c> is language-neutral plain text
    /// and passes through unchanged — running it through the resolver is kept
    /// only so legacy rows that were JSON-wrapped by the old create mapper
    /// still render as a bare code. <see cref="ILocalizationService.Get{T}"/>
    /// treats a non-JSON value as a single-culture literal.
    /// </summary>
    private CourseResponse Localize(CourseResponse response)
    {
        response.Code = _localization.Get<string>(response.Code);
        response.Title = _localization.Get<string>(response.Title);
        return response;
    }

    public async Task<Guid> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        if (await _courses.CodeExistsAsync(request.Code, cancellationToken: cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.Courses.CodeInUse);
        }

        var course = _mapper.MapToEntity(request);
        await _courses.AddAsync(course, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // A new catalog entry must show up in active-list / search reads.
        await BumpCatalogVersionAsync(cancellationToken);
        return course.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync((id, request), cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.EnsureMutable();

        if (request.Title != null) course.Title = LocalizedJson.Normalize(request.Title);
        _mapper.ApplyUpdate(request, course);
        course.UpdatedAt = DateTime.UtcNow;

        _courses.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Drop the shared object payload — next read repopulates with new values.
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCatalogVersionAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.EnsureMutable();

        // M1 guard — a catalog course bound into any academic plan cannot be
        // hard-deleted. Surfaces a clean Conflict instead of orphaning the
        // composition rows (no DB FK historically) or, post-FK, a raw 500 from
        // the FK violation. The DB FK (Restrict) remains the schema backstop.
        if (await _courses.IsReferencedByPlanAsync(id, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.Courses.ReferencedByPlan);
        }

        _courses.Delete(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCatalogVersionAsync(cancellationToken);
    }

    public async Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(ids.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in ids.Distinct())
        {
            try
            {
                await DeleteAsync(id, cancellationToken);
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

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.Close();
        _courses.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCatalogVersionAsync(cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.Reopen();
        _courses.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCatalogVersionAsync(cancellationToken);
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";
}
