using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.Courses.Mappings;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Courses;

/// <summary>
/// Bundle of the FluentValidation validators used by <see cref="AcademicPlanService"/>.
/// Keeps the service constructor under the 7-parameter limit without sacrificing
/// the per-validator explicit injection.
/// </summary>
public sealed record AcademicPlanValidators(
    IValidator<CreateAcademicPlanRequest> Create,
    IValidator<(Guid Id, UpdateAcademicPlanRequest Request)> Update,
    IValidator<AddPlanCourseRequest> AddCourse);

/// <summary>
/// Manages academic plans + their course composition. Cache strategy:
///   <list type="bullet">
///     <item><c>academicplan:object:{id}</c> — full plan + composition, per
///       <c>docs/caching-strategy.md</c> shared-object layer.</item>
///     <item>Invalidated on every plan-level OR composition-level mutation
///       (the composition is part of the plan's read model).</item>
///   </list>
/// </summary>
public class AcademicPlanService : IAcademicPlanService
{
    internal const string CacheKeyPrefix = "academicplan:object:";
    private const string AcademicPlanNotFound = LocalizedKeys.Courses.PlanNotFound;
    private const string PlanCourseEntryNotFound = LocalizedKeys.Courses.PlanCourseEntryNotFound;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAcademicPlanRepository _plans;
    private readonly ICourseRepository _courses;
    private readonly AcademicPlanValidators _validators;
    private readonly ICacheService _cache;
    private readonly IEffectiveScope _scope;
    private readonly ILocalizationService _localization;
    private readonly AcademicPlanMapper _mapper = new();

    public AcademicPlanService(
        IUnitOfWork unitOfWork,
        IAcademicPlanRepository plans,
        ICourseRepository courses,
        AcademicPlanValidators validators,
        ICacheService cache,
        IEffectiveScope scope,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _plans = plans;
        _courses = courses;
        _validators = validators;
        _cache = cache;
        _scope = scope;
        _localization = localization;
    }

    public async Task<AcademicPlanResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);

        // P1.1 — cached projection carries StructureNodeId; scope is enforced
        // against the owning node on every read so the shared cache cannot
        // leak across callers. Out-of-scope returns null → controller 404.
        // The cache stores the culture-neutral payload (Name still in the
        // {"ar":"…","en":"…"} JSON shape). Decoding runs on the way out so two
        // requests under different cultures share one cache entry.
        var cached = await _cache.GetAsync<AcademicPlanResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return await _scope.CanAccessStructureNodeAsync(cached.StructureNodeId, cancellationToken)
                ? Localize(cached)
                : null;
        }

        var plan = await _plans.GetByIdAsync(id, includeCourses: true, cancellationToken);
        if (plan is null) return null;

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken)) return null;

        var dto = ToResponse(plan);
        await _cache.SetAsync(key, dto, CacheTtl, cancellationToken);
        return Localize(dto);
    }

    public async Task<IReadOnlyList<AcademicPlanResponse>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStructureNodeAsync(structureNodeId, cancellationToken))
        {
            return Array.Empty<AcademicPlanResponse>();
        }

        var plans = await _plans.GetForStructureNodeAsync(structureNodeId, cancellationToken);
        // List read does not eager-load PlanCourses (avoids N+1) — list responses
        // are slim summaries; callers re-fetch by ID for full composition.
        return plans.Select(p => Localize(new AcademicPlanResponse
        {
            Id = p.Id,
            StructureNodeId = p.StructureNodeId,
            Name = p.Name,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            IsActive = p.IsActive,
        })).ToList();
    }

    /// <summary>
    /// Decode the bilingual <c>Name</c> on an <see cref="AcademicPlanResponse"/>.
    /// Plan composition (PlanCourses) carries only ids + numeric fields, so
    /// no decoding is needed there.
    /// </summary>
    private AcademicPlanResponse Localize(AcademicPlanResponse response)
    {
        response.Name = _localization.Get<string>(response.Name);
        return response;
    }

    public async Task<Guid> CreateAsync(CreateAcademicPlanRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Create.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        // Reject creation targeted at a node the caller cannot see.
        if (!await _scope.CanAccessStructureNodeAsync(request.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Courses.StructureNodeNotFound);
        }

        var plan = _mapper.MapToEntity(request);
        await _plans.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateAcademicPlanRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Update.ValidateAsync((id, request), cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var plan = await _plans.GetByIdAsync(id, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException(AcademicPlanNotFound);

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(AcademicPlanNotFound);
        }

        plan.EnsureMutable();

        _mapper.ApplyUpdate(request, plan);

        if (plan.EffectiveTo.HasValue && plan.EffectiveTo <= plan.EffectiveFrom)
        {
            throw new ValidationException("EffectiveTo", LocalizedKeys.Courses.EffectiveToAfterFrom);
        }

        plan.UpdatedAt = DateTime.UtcNow;
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdAsync(id, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException(AcademicPlanNotFound);

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(AcademicPlanNotFound);
        }

        plan.EnsureMutable();

        _plans.Delete(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task<Guid> AddCourseAsync(Guid planId, AddPlanCourseRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.AddCourse.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var plan = await _plans.GetByIdAsync(planId, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException(AcademicPlanNotFound);

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(AcademicPlanNotFound);
        }

        plan.EnsureMutable();

        var course = await _courses.GetByIdAsync(request.CourseId, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

        if (await _plans.ContainsCourseAsync(planId, request.CourseId, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.Courses.PlanCourseAlreadyPresent);
        }

        var entry = new AcademicPlanCourse
        {
            AcademicPlanId = planId,
            CourseId = course.Id,
            Level = request.Level,
            Semester = request.Semester,
            IsMandatory = request.IsMandatory,
        };
        plan.PlanCourses.Add(entry);
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(planId), cancellationToken);
        return entry.Id;
    }

    public async Task RemoveCourseAsync(Guid planId, Guid planCourseId, CancellationToken cancellationToken = default)
    {
        var entry = await _plans.GetPlanCourseAsync(planCourseId, cancellationToken)
            ?? throw new NotFoundException(PlanCourseEntryNotFound);

        if (entry.AcademicPlanId != planId)
        {
            throw new NotFoundException(PlanCourseEntryNotFound);
        }

        // Resolve owning plan to scope-check the removal; the entry itself
        // doesn't carry StructureNodeId.
        var plan = await _plans.GetByIdAsync(planId, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException(PlanCourseEntryNotFound);
        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(PlanCourseEntryNotFound);
        }

        plan.EnsureMutable();

        _plans.RemovePlanCourse(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(planId), cancellationToken);
    }

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await LoadForWriteAsync(id, cancellationToken);
        plan.Close();
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await LoadForWriteAsync(id, cancellationToken);
        plan.Reopen();
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    /// <summary>
    /// Fetch a tracked plan by id and require the caller's scope to cover
    /// its owning structure node.
    /// </summary>
    private async Task<AcademicPlan> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _plans.GetByIdAsync(id, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException(AcademicPlanNotFound);

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(AcademicPlanNotFound);
        }
        return plan;
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    private AcademicPlanResponse ToResponse(AcademicPlan plan)
    {
        var dto = _mapper.MapToResponse(plan);
        dto.PlanCourses = plan.PlanCourses
            .OrderBy(pc => pc.Level).ThenBy(pc => pc.Semester)
            .Select(_mapper.MapToCourseResponse)
            .ToList();
        return dto;
    }

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
