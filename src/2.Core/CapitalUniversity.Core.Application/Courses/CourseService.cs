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
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

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
        var key = CacheKey(id);
        // Cache stores the culture-neutral response (Title still in
        // {"ar":"…","en":"…"} shape). Decoding happens on the way out so two
        // requests with different Accept-Language hit the same cache entry
        // without poisoning each other.
        var cached = await _cache.GetAsync<CourseResponse>(key, cancellationToken);
        if (cached is not null) return Localize(cached);

        var course = await _courses.GetByIdAsync(id, cancellationToken);
        if (course is null) return null;

        var response = _mapper.MapToResponse(course);
        await _cache.SetAsync(key, response, CacheTtl, cancellationToken);
        return Localize(response);
    }

    public async Task<IReadOnlyList<CourseResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _courses.GetActiveAsync(cancellationToken);
        return courses.Select(c => Localize(_mapper.MapToResponse(c))).ToList();
    }

    public async Task<PagedResult<CourseResponse>> SearchAsync(CourseSearchQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _courses.SearchAsync(query, cancellationToken);
        return new PagedResult<CourseResponse>
        {
            Items = page.Items.Select(c => Localize(_mapper.MapToResponse(c))).ToList(),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    /// <summary>
    /// Decode the bilingual <c>Code</c> and <c>Title</c> fields on a
    /// <see cref="CourseResponse"/> against the current culture. Plain-text
    /// rows pass through unchanged — <see cref="ILocalizationService.Get{T}"/>
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
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.EnsureMutable();

        _courses.Delete(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
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
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        course.Reopen();
        _courses.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";
}
