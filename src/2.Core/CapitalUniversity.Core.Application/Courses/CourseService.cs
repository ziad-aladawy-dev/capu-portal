using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
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
    private readonly CourseMapper _mapper = new();

    public CourseService(
        IUnitOfWork unitOfWork,
        ICourseRepository courses,
        IValidator<CreateCourseRequest> createValidator,
        IValidator<(Guid Id, UpdateCourseRequest Request)> updateValidator,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _courses = courses;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _cache = cache;
    }

    public async Task<CourseResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);
        var cached = await _cache.GetAsync<CourseResponse>(key, cancellationToken);
        if (cached is not null) return cached;

        var course = await _courses.GetByIdAsync(id, cancellationToken);
        if (course is null) return null;

        var response = _mapper.MapToResponse(course);
        await _cache.SetAsync(key, response, CacheTtl, cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<CourseResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _courses.GetActiveAsync(cancellationToken);
        return courses.Select(_mapper.MapToResponse).ToList();
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
            throw new ConflictException($"Course code '{request.Code}' is already in use.");
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
            ?? throw new NotFoundException("Course not found.");

        if (!string.IsNullOrWhiteSpace(request.Title)) course.Title = request.Title;
        if (request.CreditHours.HasValue) course.CreditHours = request.CreditHours.Value;
        if (request.Category.HasValue) course.Category = request.Category.Value;
        if (request.IsActive.HasValue) course.IsActive = request.IsActive.Value;
        course.UpdatedAt = DateTime.UtcNow;

        _courses.Update(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Drop the shared object payload — next read repopulates with new values.
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var course = await _courses.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Course not found.");

        _courses.Delete(course);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";
}
