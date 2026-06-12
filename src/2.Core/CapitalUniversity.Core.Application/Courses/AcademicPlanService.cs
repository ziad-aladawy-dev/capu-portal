using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
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
    // Collection caches (per-node list + search pages) keyed by a collection
    // version stamp; any plan or composition mutation rotates it and orphans them
    // all in one write — mirrors the Courses module and the permission epoch model.
    internal const string CollectionVersionKey = "academicplan:coll:ver";
    internal const string NodeListKeyPrefix = "academicplan:node:";
    internal const string SearchKeyPrefix = "academicplan:search:";
    private const string AcademicPlanNotFound = LocalizedKeys.Courses.PlanNotFound;
    private const string PlanCourseEntryNotFound = LocalizedKeys.Courses.PlanCourseEntryNotFound;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    // The stamp only rotates on mutation, so it long-outlives the data TTL.
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

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
        // Stampede-protected read-through over the scope-neutral payload; scope is
        // still enforced per read (below) so the shared entry never leaks across
        // callers. A not-found plan returns null and is not cached.
        var dto = await _cache.GetOrSetAsync<AcademicPlanResponse>(
            key,
            async ct =>
            {
                var plan = await _plans.GetByIdAsync(id, includeCourses: true, ct);
                return plan is null ? null : ToResponse(plan);
            },
            CacheTtl,
            cancellationToken);

        if (dto is null) return null;

        return await _scope.CanAccessStructureNodeAsync(dto.StructureNodeId, cancellationToken)
            ? Localize(dto)
            : null;
    }

    public async Task<PagedResult<AcademicPlanResponse>> SearchAsync(AcademicPlanSearchQuery query, CancellationToken cancellationToken = default)
    {
        // If pinned to a node, scope-check once and short-circuit on miss.
        if (query.StructureNodeId.HasValue &&
            !await _scope.CanAccessStructureNodeAsync(query.StructureNodeId.Value, cancellationToken))
        {
            return new PagedResult<AcademicPlanResponse>
            {
                Items = new List<AcademicPlanResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0,
                TotalPages = 0,
            };
        }

        // Cache the scope-NEUTRAL page (all matches + unfiltered totals) under the
        // collection version + query signature. The per-row scope filter runs per
        // read, so the expensive DB query is shared while each caller still sees
        // only the rows their scope permits (preserving the prior behaviour of
        // unfiltered totals with filtered items).
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cacheKey = $"{SearchKeyPrefix}{version}:{BuildSearchSignature(query)}";

        var page = await _cache.GetOrSetAsync<PagedResult<AcademicPlanResponse>>(
            cacheKey,
            async ct =>
            {
                var result = await _plans.SearchAsync(query, ct);
                return new PagedResult<AcademicPlanResponse>
                {
                    Items = result.Items.Select(ToSlim).ToList(),
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
            return new PagedResult<AcademicPlanResponse>
            {
                Items = new List<AcademicPlanResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0,
                TotalPages = 0,
            };
        }

        var visible = new List<AcademicPlanResponse>(page.Items.Count);
        foreach (var p in page.Items)
        {
            if (!await _scope.CanAccessStructureNodeAsync(p.StructureNodeId, cancellationToken)) continue;
            visible.Add(LocalizeCopy(p));
        }

        return new PagedResult<AcademicPlanResponse>
        {
            Items = visible,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    public async Task<IReadOnlyList<AcademicPlanResponse>> GetForStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default)
    {
        // Per-user scope gate stays OUT of the cache (it is caller-specific).
        if (!await _scope.CanAccessStructureNodeAsync(structureNodeId, cancellationToken))
        {
            return Array.Empty<AcademicPlanResponse>();
        }

        // List read does not eager-load PlanCourses (avoids N+1) — list responses
        // are slim summaries; callers re-fetch by ID for full composition. The
        // node's list is the same for every caller that can see the node, so it is
        // cached (scope-neutral) under the collection version, stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<AcademicPlanResponse>>(
            $"{NodeListKeyPrefix}{structureNodeId:N}:{version}",
            async ct => (await _plans.GetForStructureNodeAsync(structureNodeId, ct)).Select(ToSlim).ToList(),
            CacheTtl,
            cancellationToken);

        return (cached ?? new List<AcademicPlanResponse>()).Select(LocalizeCopy).ToList();
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

    /// <summary>
    /// Localize onto a NEW response so cached list/search entries (possibly the
    /// same instance under the in-memory provider) are never mutated in place.
    /// </summary>
    private AcademicPlanResponse LocalizeCopy(AcademicPlanResponse source) => new()
    {
        Id = source.Id,
        StructureNodeId = source.StructureNodeId,
        Name = _localization.Get<string>(source.Name),
        EffectiveFrom = source.EffectiveFrom,
        EffectiveTo = source.EffectiveTo,
        IsActive = source.IsActive,
        IsClosed = source.IsClosed,
    };

    /// <summary>Slim list/search projection — no PlanCourses (avoids N+1).</summary>
    private static AcademicPlanResponse ToSlim(AcademicPlan p) => new()
    {
        Id = p.Id,
        StructureNodeId = p.StructureNodeId,
        Name = p.Name,
        EffectiveFrom = p.EffectiveFrom,
        EffectiveTo = p.EffectiveTo,
        IsActive = p.IsActive,
        IsClosed = p.IsClosed,
    };

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCollectionVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    private static string BuildSearchSignature(AcademicPlanSearchQuery q) =>
        string.Join('|',
            $"node={q.StructureNodeId?.ToString("N") ?? string.Empty}",
            $"active={q.IsActive?.ToString() ?? string.Empty}",
            $"from={q.EffectiveFromInclusive?.ToString("O") ?? string.Empty}",
            $"to={q.EffectiveToExclusive?.ToString("O") ?? string.Empty}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

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
        // A new plan must surface in node-list / search reads.
        await BumpCollectionVersionAsync(cancellationToken);
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

        if (request.Name != null) plan.Name = LocalizedJson.Normalize(request.Name);
        _mapper.ApplyUpdate(request, plan);

        if (plan.EffectiveTo.HasValue && plan.EffectiveTo <= plan.EffectiveFrom)
        {
            throw new ValidationException("EffectiveTo", LocalizedKeys.Courses.EffectiveToAfterFrom);
        }

        plan.UpdatedAt = DateTime.UtcNow;
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
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
        await BumpCollectionVersionAsync(cancellationToken);
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
        // Track the new child EXPLICITLY as Added. BaseEntity pre-assigns the
        // Guid key, so relationship-fixup discovery would track it as Modified
        // (UPDATE on a row that doesn't exist → DbUpdateConcurrencyException).
        // Deliberately NOT calling _plans.Update(plan): that would force a
        // rewrite of the (unchanged) plan row under its RowVersion guard,
        // making concurrent composition edits conflict with each other for no reason.
        _plans.AddPlanCourse(entry);
        plan.PlanCourses.Add(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(planId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
        return entry.Id;
    }

    public async Task BatchUpdateCoursesAsync(Guid planId, BatchPlanCoursesRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request", LocalizedKeys.Infrastructure.Required);
        if (request.Add.Count == 0 && request.Remove.Count == 0)
        {
            // Empty diff is a successful no-op — saves callers from special-casing.
            return;
        }

        var plan = await _plans.GetByIdAsync(planId, includeCourses: true, cancellationToken)
            ?? throw new NotFoundException(AcademicPlanNotFound);

        if (!await _scope.CanAccessStructureNodeAsync(plan.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(AcademicPlanNotFound);
        }
        plan.EnsureMutable();

        // -- Validate the additions in one pass before mutating anything ----
        // All-or-nothing means we want every step to be pre-checked so the
        // first SaveChanges below either applies the full diff or none of it.
        foreach (var add in request.Add)
        {
            var validation = await _validators.AddCourse.ValidateAsync(add, cancellationToken);
            if (!validation.IsValid) throw ValidationFrom(validation);
        }

        // Duplicate detection inside the request itself — two adds for the
        // same course in one batch would otherwise both pass ContainsCourseAsync.
        if (request.Add
            .GroupBy(a => a.CourseId)
            .Any(g => g.Count() > 1))
        {
            throw new ConflictException(LocalizedKeys.Courses.PlanCourseAlreadyPresent);
        }

        // Removals must each point at an existing entry on THIS plan.
        var removals = new List<AcademicPlanCourse>(request.Remove.Count);
        foreach (var planCourseId in request.Remove.Distinct())
        {
            var entry = plan.PlanCourses.FirstOrDefault(pc => pc.Id == planCourseId)
                ?? throw new NotFoundException(PlanCourseEntryNotFound);
            removals.Add(entry);
        }

        // -- Apply --------------------------------------------------------
        foreach (var entry in removals)
        {
            _plans.RemovePlanCourse(entry);
            plan.PlanCourses.Remove(entry);
        }

        foreach (var add in request.Add)
        {
            // Existence check on the catalog course.
            var course = await _courses.GetByIdAsync(add.CourseId, cancellationToken)
                ?? throw new NotFoundException(LocalizedKeys.Courses.NotFound);

            // Re-check against the post-removal composition so an "add + remove
            // of the same course" pair in one batch is legal.
            if (plan.PlanCourses.Any(pc => pc.CourseId == add.CourseId))
            {
                throw new ConflictException(LocalizedKeys.Courses.PlanCourseAlreadyPresent);
            }

            var entry = new AcademicPlanCourse
            {
                AcademicPlanId = planId,
                CourseId = course.Id,
                Level = add.Level,
                Semester = add.Semester,
                IsMandatory = add.IsMandatory,
            };
            // Explicit Add — see AddCourseAsync: fixup discovery would track
            // the pre-keyed child as Modified, not Added.
            _plans.AddPlanCourse(entry);
            plan.PlanCourses.Add(entry);
        }

        // Composition changes are INSERTs/DELETEs on AcademicPlanCourses only —
        // the plan row itself is untouched. _plans.Update(plan) here would mark
        // every plan column modified and emit an UPDATE guarded by RowVersion,
        // so two overlapping batch edits (e.g. rapid drag-drops in the
        // curriculum grid) would collide on the parent row and surface as
        // DbUpdateConcurrencyException even though they touch different
        // courses.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(planId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
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
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await LoadForWriteAsync(id, cancellationToken);
        plan.Close();
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await LoadForWriteAsync(id, cancellationToken);
        plan.Reopen();
        _plans.Update(plan);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
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
