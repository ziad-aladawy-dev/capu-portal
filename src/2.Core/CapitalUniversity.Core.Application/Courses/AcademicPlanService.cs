using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Application.Courses.Mappings;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Courses;

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
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAcademicPlanRepository _plans;
    private readonly ICourseRepository _courses;
    private readonly IValidator<CreateAcademicPlanRequest> _createValidator;
    private readonly IValidator<(Guid Id, UpdateAcademicPlanRequest Request)> _updateValidator;
    private readonly IValidator<AddPlanCourseRequest> _addCourseValidator;
    private readonly ICacheService _cache;
    private readonly AcademicPlanMapper _mapper = new();

    public AcademicPlanService(
        IUnitOfWork unitOfWork,
        IAcademicPlanRepository plans,
        ICourseRepository courses,
        IValidator<CreateAcademicPlanRequest> createValidator,
        IValidator<(Guid Id, UpdateAcademicPlanRequest Request)> updateValidator,
        IValidator<AddPlanCourseRequest> addCourseValidator,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _plans = plans;
        _courses = courses;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addCourseValidator = addCourseValidator;
        _cache = cache;
    }

    public async Task<AcademicPlanResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);
        var cached = await _cache.GetAsync<AcademicPlanResponse>(key, cancellationToken);
        if (cached is not null) return cached;

        var plan = await _plans.GetByIdAsync(id, includeCourses: true, cancellationToken);
        if (plan is null) return null;

        var dto = ToResponse(plan);
        await _cache.SetAsync(key, dto, CacheTtl, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<AcademicPlanResponse>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default)
    {
        var plans = await _plans.GetForStructureNodeAsync(structureNodeId, cancellationToken);
        // List read does not eager-load PlanCourses (avoids N+1) — list responses
        // are slim summaries; callers re-fetch by ID for full composition.
        return plans.Select(p => new AcademicPlanResponse
        {
            Id = p.Id,
            StructureNodeId = p.StructureNodeId,
            Name = p.Name,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            IsActive = p.IsActive,
        }).ToList();
    }

    public async Task<Guid> CreateAsync(CreateAcademicPlanRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var plan = _mapper.MapToEntity(request);
        await _plans.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateAcademicPlanRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync((id, request), cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var plan = await _plans.GetByIdAsync(id, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException("Academic plan not found.");

        if (!string.IsNullOrWhiteSpace(request.Name)) plan.Name = request.Name;
        if (request.EffectiveFrom.HasValue) plan.EffectiveFrom = request.EffectiveFrom.Value;
        if (request.EffectiveTo.HasValue) plan.EffectiveTo = request.EffectiveTo;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;

        if (plan.EffectiveTo.HasValue && plan.EffectiveTo <= plan.EffectiveFrom)
        {
            throw new ValidationException("EffectiveTo", "EffectiveTo must be greater than EffectiveFrom.");
        }

        plan.UpdatedAt = DateTime.UtcNow;
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _plans.GetByIdAsync(id, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException("Academic plan not found.");

        _plans.Delete(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task<Guid> AddCourseAsync(Guid planId, AddPlanCourseRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _addCourseValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var plan = await _plans.GetByIdAsync(planId, includeCourses: false, cancellationToken)
            ?? throw new NotFoundException("Academic plan not found.");

        var course = await _courses.GetByIdAsync(request.CourseId, cancellationToken)
            ?? throw new NotFoundException("Course not found.");

        if (await _plans.ContainsCourseAsync(planId, request.CourseId, cancellationToken))
        {
            throw new ConflictException("Course already present in this plan.");
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
            ?? throw new NotFoundException("Plan course entry not found.");

        if (entry.AcademicPlanId != planId)
        {
            throw new NotFoundException("Plan course entry not found.");
        }

        _plans.RemovePlanCourse(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(planId), cancellationToken);
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
