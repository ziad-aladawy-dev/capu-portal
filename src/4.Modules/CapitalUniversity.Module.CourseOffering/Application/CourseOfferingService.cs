using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentValidation;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.CourseOffering.Application;

/// <summary>
/// Owns the create + update lifecycle for <see cref="CourseOfferingEntity"/>.
/// Scope is enforced against the offering's <c>StructureNodeId</c> — out-of-scope
/// reads return <c>null</c> and mutations throw <see cref="NotFoundException"/>
/// to avoid leaking existence (consistent with InvoiceService / AcademicPlanService).
///
/// <para>
/// Deliberately narrow: no registration orchestration, no schedule conflict
/// logic, no fee / transcript coupling. Future Registration / Scheduling
/// modules consume an offering by id and apply their own policies.
/// </para>
/// </summary>
public class CourseOfferingService : ICourseOfferingService
{
    // Shared-object cache key + collection caches (per node-semester / course-
    // semester list + search pages) keyed by a version stamp; any offering
    // mutation rotates it. Matches docs/caching-strategy.md and the Courses /
    // AcademicPlans / Schedule modules. CourseOfferingResponse carries no
    // localizable fields, so cached payloads need no per-culture handling.
    internal const string CacheKeyPrefix = "courseoffering:object:";
    internal const string CollectionVersionKey = "courseoffering:coll:ver";
    internal const string NodeSemesterKeyPrefix = "courseoffering:node-sem:";
    internal const string CourseSemesterKeyPrefix = "courseoffering:course-sem:";
    internal const string SearchKeyPrefix = "courseoffering:search:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourseOfferingRepository _offerings;
    private readonly IValidator<CreateCourseOfferingRequest> _createValidator;
    private readonly IValidator<UpdateCourseOfferingRequest> _updateValidator;
    private readonly IEffectiveScope _scope;
    private readonly ICacheService _cache;

    public CourseOfferingService(
        IUnitOfWork unitOfWork,
        ICourseOfferingRepository offerings,
        IValidator<CreateCourseOfferingRequest> createValidator,
        IValidator<UpdateCourseOfferingRequest> updateValidator,
        IEffectiveScope scope,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _offerings = offerings;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _scope = scope;
        _cache = cache;
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    public async Task<CourseOfferingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Stampede-protected shared-object read-through; scope enforced per read.
        var dto = await _cache.GetOrSetAsync<CourseOfferingResponse>(
            CacheKey(id),
            async ct =>
            {
                var offering = await _offerings.GetByIdAsync(id, ct);
                return offering is null ? null : ToResponse(offering);
            },
            CacheTtl,
            cancellationToken);

        if (dto is null) return null;
        if (!await _scope.CanAccessStructureNodeAsync(dto.StructureNodeId, cancellationToken)) return null;
        if (!await _scope.CanAccessSemesterAsync(dto.SemesterId, cancellationToken)) return null;
        return dto;
    }

    public async Task<IReadOnlyList<CourseOfferingResponse>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStructureNodeAsync(structureNodeId, cancellationToken))
        {
            return Array.Empty<CourseOfferingResponse>();
        }

        if (!await _scope.CanAccessSemesterAsync(semesterId, cancellationToken))
        {
            return Array.Empty<CourseOfferingResponse>();
        }

        // Same offerings for every caller that can see the node+semester, so
        // cached scope-neutral under the collection version. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var statusKey = status.HasValue ? ((int)status.Value).ToString() : "all";
        var cached = await _cache.GetOrSetAsync<List<CourseOfferingResponse>>(
            $"{NodeSemesterKeyPrefix}{structureNodeId:N}:{semesterId:N}:{statusKey}:{version}",
            async ct => (await _offerings.GetForNodeSemesterAsync(structureNodeId, semesterId, status, ct)).Select(ToResponse).ToList(),
            CacheTtl,
            cancellationToken);

        return new List<CourseOfferingResponse>(cached ?? new List<CourseOfferingResponse>());
    }

    public async Task<IReadOnlyList<CourseOfferingResponse>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessSemesterAsync(semesterId, cancellationToken))
        {
            return Array.Empty<CourseOfferingResponse>();
        }

        // Cache the scope-NEUTRAL list (all offerings for course+semester) under
        // the collection version; the per-node visibility filter runs on read so
        // each caller sees only permitted rows while the DB query is shared.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<CourseOfferingResponse>>(
            $"{CourseSemesterKeyPrefix}{courseId:N}:{semesterId:N}:{version}",
            async ct => (await _offerings.GetForCourseAsync(courseId, semesterId, ct)).Select(ToResponse).ToList(),
            CacheTtl,
            cancellationToken);

        var rows = cached ?? new List<CourseOfferingResponse>();
        if (rows.Count == 0) return Array.Empty<CourseOfferingResponse>();

        // Cross-node query — filter each row by per-node visibility so an admin
        // sees only the offerings their scope grants. Avoids broadcasting that
        // a course is running under a node they cannot otherwise see.
        var visible = new List<CourseOfferingResponse>(rows.Count);
        foreach (var offering in rows)
        {
            if (await _scope.CanAccessStructureNodeAsync(offering.StructureNodeId, cancellationToken))
            {
                visible.Add(offering);
            }
        }
        return visible;
    }

    public async Task<Guid> CreateAsync(CreateCourseOfferingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        if (!await _scope.CanAccessStructureNodeAsync(request.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Courses.StructureNodeNotFound);
        }

        if (!await _scope.CanAccessSemesterAsync(request.SemesterId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        if (await _offerings.SectionExistsAsync(request.CourseId, request.SemesterId, request.StructureNodeId, request.SectionCode, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.CourseOfferings.SectionInUse);
        }

        var offering = new CourseOfferingEntity
        {
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            StructureNodeId = request.StructureNodeId,
            SectionCode = request.SectionCode,
            Status = request.Status,
            RegistrationState = request.RegistrationState,
            InstructorId = NormalizeInstructor(request.InstructorId),
            // DTO retains the legacy ExternalSystemId name; entity now stores
            // it on the composed ExternallySourced.ExternalId block (same
            // physical column).
            ExternallySourced = new() { ExternalId = request.ExternalSystemId },
        };
        offering.InitializeCapacity(request.Capacity);

        await _offerings.AddAsync(offering, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // A new offering must surface in node-semester / course-semester / search reads.
        await BumpCollectionVersionAsync(cancellationToken);
        return offering.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateCourseOfferingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var offering = await LoadForWriteAsync(id, cancellationToken);

        // Each Apply* helper carries its own "only act if the field was sent"
        // guard and its own exception-translation logic. Order matters:
        // Status before RegistrationState (the registration-state guard reads
        // post-transition Status). The rest are independent.
        await ApplySectionCodeAsync(offering, request, cancellationToken);
        ApplyCapacity(offering, request);
        ApplyStatusChange(offering, request);
        ApplyRegistrationStateChange(offering, request);
        ApplyInstructor(offering, request);
        ApplyExternalSyncMetadata(offering, request);

        offering.UpdatedAt = DateTime.UtcNow;
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Drop the shared object payload + rotate the collection version so all
        // list/search reads reflect the change.
        await _cache.RemoveAsync(CacheKey(offering.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    /// <summary>
    /// Section code is the only field whose uniqueness has to be re-verified
    /// against siblings before mutation. Skips if the request didn't include
    /// the field or the value is unchanged.
    /// </summary>
    private async Task ApplySectionCodeAsync(CourseOfferingEntity offering, UpdateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        if (request.SectionCode is null || request.SectionCode == offering.SectionCode) return;

        if (await _offerings.SectionExistsAsync(offering.CourseId, offering.SemesterId, offering.StructureNodeId, request.SectionCode, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.CourseOfferings.SectionInUse);
        }
        offering.SectionCode = request.SectionCode;
    }

    /// <summary>
    /// Capacity changes route through the entity's <c>AdjustCapacity</c>
    /// invariant guard. The entity throws <see cref="InvalidOperationException"/>
    /// when the new value falls below the current registered count; we surface
    /// that as a conflict so the controller maps to 409 with the localized key.
    /// </summary>
    private static void ApplyCapacity(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.Capacity.HasValue) return;

        try { offering.AdjustCapacity(request.Capacity.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.CapacityBelowCount); }
    }

    /// <summary>
    /// Status transition. Same-state requests are silent no-ops so a partial
    /// update payload echoing the current status doesn't trip the lifecycle
    /// guards. Illegal transitions (e.g. Closed → Open) surface as conflict.
    /// </summary>
    private static void ApplyStatusChange(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.Status.HasValue || request.Status.Value == offering.Status) return;

        try { ApplyStatus(offering, request.Status.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.IllegalStateTransition); }
    }

    /// <summary>
    /// Registration-state transition. Same-state is a silent no-op (matches
    /// the Status path). The entity's per-state guards (e.g. Open requires
    /// <c>Status == Open</c>) surface as conflict.
    /// </summary>
    private static void ApplyRegistrationStateChange(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.RegistrationState.HasValue || request.RegistrationState.Value == offering.RegistrationState) return;

        try { ApplyRegistrationState(offering, request.RegistrationState.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.IllegalStateTransition); }
    }

    /// <summary>
    /// Instructor assignment with PATCH-sentinel semantics: omitted/null =
    /// unchanged, <c>Guid.Empty</c> = clear, any other value = assign. The id
    /// is a loose pointer to Staff (no FK, no existence check here — the staff
    /// directory is a different module; the UI picks from a live list).
    /// </summary>
    private static void ApplyInstructor(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.InstructorId.HasValue) return;
        offering.InstructorId = NormalizeInstructor(request.InstructorId);
    }

    private static Guid? NormalizeInstructor(Guid? instructorId) =>
        instructorId.HasValue && instructorId.Value == Guid.Empty ? null : instructorId;

    /// <summary>
    /// Passive external-sync metadata. No invariants — just per-field
    /// "set when sent" semantics. Kept as its own helper so the orchestration
    /// reads as one statement per concern.
    /// </summary>
    private static void ApplyExternalSyncMetadata(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        // DTO retains the legacy ExternalSystemId/ExternalSyncedAt names; entity
        // now stores them on the composed ExternallySourced block
        // (ExternallySourced.ExternalId + ExternallySourced.LastSyncedAt).
        if (request.ExternalSystemId is not null) offering.ExternallySourced.ExternalId = request.ExternalSystemId;
        if (request.ExternalSyncedAt.HasValue) offering.ExternallySourced.LastSyncedAt = request.ExternalSyncedAt;
    }

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCollectionVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    private static string BuildSearchSignature(CourseOfferingSearchQuery q) =>
        string.Join('|',
            $"sem={q.SemesterId?.ToString("N") ?? string.Empty}",
            $"node={q.StructureNodeId?.ToString("N") ?? string.Empty}",
            $"course={q.CourseId?.ToString("N") ?? string.Empty}",
            $"status={(q.Status.HasValue ? ((int)q.Status.Value).ToString() : string.Empty)}",
            $"reg={(q.RegistrationState.HasValue ? ((int)q.RegistrationState.Value).ToString() : string.Empty)}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

    internal static CourseOfferingResponse ToResponse(CourseOfferingEntity o) => new()
    {
        Id = o.Id,
        CourseId = o.CourseId,
        SemesterId = o.SemesterId,
        StructureNodeId = o.StructureNodeId,
        SectionCode = o.SectionCode,
        Capacity = o.Capacity,
        RegisteredCount = o.RegisteredCount,
        Status = o.Status,
        RegistrationState = o.RegistrationState,
        InstructorId = o.InstructorId,
        // Legacy DTO field names; mapped from the composed ExternallySourced block.
        ExternalSystemId = o.ExternallySourced.ExternalId,
        ExternalSyncedAt = o.ExternallySourced.LastSyncedAt,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
    };

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await LoadForWriteAsync(id, cancellationToken);
        offering.Close();
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Drop the shared object payload + rotate the collection version so all
        // list/search reads reflect the change.
        await _cache.RemoveAsync(CacheKey(offering.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await LoadForWriteAsync(id, cancellationToken);
        offering.Reopen();
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Drop the shared object payload + rotate the collection version so all
        // list/search reads reflect the change.
        await _cache.RemoveAsync(CacheKey(offering.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    /// <summary>
    /// Fetch a tracked offering by id and require the caller's scope to cover
    /// its owning structure node. Single helper so every write path uses the
    /// same miss / out-of-scope handling — both map to <see cref="NotFoundException"/>
    /// with the same key to avoid leaking existence (P1.1 in the project's
    /// remediation plan).
    /// </summary>
    private async Task<CourseOfferingEntity> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var offering = await _offerings.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);

        if (!await _scope.CanAccessStructureNodeAsync(offering.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        if (!await _scope.CanAccessSemesterAsync(offering.SemesterId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        offering.EnsureMutable();

        return offering;
    }

    private static void ApplyStatus(CourseOfferingEntity offering, OfferingStatus target)
    {
        switch (target)
        {
            case OfferingStatus.Open:      offering.Activate(); break;
            case OfferingStatus.Closed:    offering.Close();    break;
            case OfferingStatus.Cancelled: offering.Cancel();   break;
            // Reverting to Draft from any non-Draft state is not a legal
            // transition — surface as the same illegal-transition conflict.
            case OfferingStatus.Draft:     throw new InvalidOperationException("Cannot revert an offering to draft.");
            default:                       throw new InvalidOperationException($"Unknown offering status: {target}.");
        }
    }

    private static void ApplyRegistrationState(CourseOfferingEntity offering, RegistrationState target)
    {
        switch (target)
        {
            case RegistrationState.Open:     offering.OpenRegistration();  break;
            case RegistrationState.Closed:   offering.CloseRegistration(); break;
            case RegistrationState.Waitlist: offering.SetWaitlist();       break;
            default: throw new InvalidOperationException($"Unknown registration state: {target}.");
        }
    }

    public async Task<PagedResult<CourseOfferingResponse>> SearchAsync(CourseOfferingSearchQuery query, CancellationToken cancellationToken = default)
    {
        // If the caller pinned a specific node, scope-check that one id; an
        // out-of-scope request returns an empty page (no existence leak).
        if (query.StructureNodeId.HasValue &&
            !await _scope.CanAccessStructureNodeAsync(query.StructureNodeId.Value, cancellationToken))
        {
            return EmptyPage(query);
        }
        if (query.SemesterId.HasValue &&
            !await _scope.CanAccessSemesterAsync(query.SemesterId.Value, cancellationToken))
        {
            return EmptyPage(query);
        }

        // Cache the scope-NEUTRAL page under the collection version + query
        // signature; per-row visibility filtering runs on read. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var page = await _cache.GetOrSetAsync<PagedResult<CourseOfferingResponse>>(
            $"{SearchKeyPrefix}{version}:{BuildSearchSignature(query)}",
            async ct =>
            {
                var result = await _offerings.SearchAsync(query, ct);
                return new PagedResult<CourseOfferingResponse>
                {
                    Items = result.Items.Select(ToResponse).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? EmptyPage(query);

        // For un-pinned cross-node queries, filter the materialized page by
        // per-row node visibility. Total stays as the raw count — admins
        // with the View permission typically have unrestricted scope. If a
        // narrower-scoped reader hits this endpoint, the page may shrink
        // post-filter; the trade-off keeps the query single-pass.
        var visible = new List<CourseOfferingResponse>(page.Items.Count);
        foreach (var o in page.Items)
        {
            if (!await _scope.CanAccessStructureNodeAsync(o.StructureNodeId, cancellationToken)) continue;
            visible.Add(o);
        }

        return new PagedResult<CourseOfferingResponse>
        {
            Items = visible,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    private static PagedResult<CourseOfferingResponse> EmptyPage(CourseOfferingSearchQuery query) => new()
    {
        Items = new List<CourseOfferingResponse>(),
        Page = query.NormalizedPage,
        PageSize = query.NormalizedPageSize,
        TotalCount = 0,
        TotalPages = 0,
    };

    /// <summary>
    /// Batch section creation. Generates section codes server-side (letter
    /// series A, B, C… or <c>{prefix}{n}</c>), skipping codes already taken by
    /// live siblings, then funnels each row through <see cref="CreateAsync"/>
    /// so every per-row guard (validator, scope, uniqueness) applies. Per-row
    /// commit — a failed section never rolls back its created peers.
    /// </summary>
    public async Task<BatchCreateOfferingsResult> BatchCreateAsync(BatchCreateCourseOfferingsRequest request, CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(request.Count, 1, 50);
        var result = new BatchCreateOfferingsResult();

        var existing = (await _offerings.GetForCourseAsync(request.CourseId, request.SemesterId, cancellationToken))
            .Where(o => o.StructureNodeId == request.StructureNodeId)
            .Select(o => o.SectionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sectionCode in GenerateSectionCodes(request.SectionPrefix, count, existing))
        {
            try
            {
                var id = await CreateAsync(new CreateCourseOfferingRequest
                {
                    CourseId = request.CourseId,
                    SemesterId = request.SemesterId,
                    StructureNodeId = request.StructureNodeId,
                    SectionCode = sectionCode,
                    Capacity = request.Capacity,
                    Status = request.Status,
                    InstructorId = request.InstructorId,
                }, cancellationToken);
                result.Created.Add(new BatchCreatedOffering { Id = id, SectionCode = sectionCode });
            }
            catch (Exception ex) when (ex is NotFoundException or ConflictException or ValidationException)
            {
                result.Failures.Add(new BatchOfferingFailure { SectionCode = sectionCode, Message = ex.Message });
            }
        }

        return result;
    }

    /// <summary>
    /// Yields <paramref name="count"/> candidate codes not present in
    /// <paramref name="taken"/>. Empty prefix → letter series (A…Z, AA, AB…);
    /// otherwise numbered prefix (EVE-1, EVE-2…).
    /// </summary>
    private static IEnumerable<string> GenerateSectionCodes(string? prefix, int count, HashSet<string> taken)
    {
        var produced = 0;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            for (var i = 0; produced < count && i < 1000; i++)
            {
                var code = ToLetterSeries(i);
                if (taken.Contains(code)) continue;
                produced++;
                yield return code;
            }
        }
        else
        {
            var p = prefix.Trim();
            for (var n = 1; produced < count && n < 1000; n++)
            {
                var code = $"{p}{n}";
                if (code.Length > 32 || taken.Contains(code)) continue;
                produced++;
                yield return code;
            }
        }
    }

    /// <summary>0 → "A", 25 → "Z", 26 → "AA" — spreadsheet column naming.</summary>
    private static string ToLetterSeries(int index)
    {
        var code = string.Empty;
        index++;
        while (index > 0)
        {
            index--;
            code = (char)('A' + index % 26) + code;
            index /= 26;
        }
        return code;
    }

    /// <summary>
    /// Aggregate counters for the admin dashboard strip. Node-pinned requests
    /// are scope-checked up front; un-pinned requests filter per row so the
    /// counters reflect only offerings the caller can see.
    /// </summary>
    public async Task<CourseOfferingStatsResponse> GetStatsAsync(Guid semesterId, Guid? structureNodeId, CancellationToken cancellationToken = default)
    {
        var stats = new CourseOfferingStatsResponse();

        if (!await _scope.CanAccessSemesterAsync(semesterId, cancellationToken)) return stats;
        if (structureNodeId.HasValue && !await _scope.CanAccessStructureNodeAsync(structureNodeId.Value, cancellationToken)) return stats;

        var rows = await _offerings.GetForSemesterAsync(semesterId, structureNodeId, cancellationToken);

        foreach (var o in rows)
        {
            if (!structureNodeId.HasValue && !await _scope.CanAccessStructureNodeAsync(o.StructureNodeId, cancellationToken)) continue;

            stats.Total++;
            switch (o.Status)
            {
                case OfferingStatus.Draft: stats.DraftCount++; break;
                case OfferingStatus.Open: stats.OpenCount++; break;
                case OfferingStatus.Closed: stats.ClosedCount++; break;
                case OfferingStatus.Cancelled: stats.CancelledCount++; break;
            }
            if (o.Capacity > 0 && o.RegisteredCount >= o.Capacity) stats.FullCount++;
            stats.TotalCapacity += o.Capacity;
            stats.TotalRegistered += o.RegisteredCount;
        }

        return stats;
    }

    public Task<BulkActionResult> BulkPublishAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => BulkApplyAsync(ids, (o, _) => o.Activate(), reason: null, cancellationToken);

    public Task<BulkActionResult> BulkCancelAsync(IReadOnlyList<Guid> ids, string reason, CancellationToken cancellationToken = default)
        => BulkApplyAsync(ids, (o, _) => o.Cancel(), reason: reason, cancellationToken);

    /// <summary>
    /// Per-row driver shared by every bulk lifecycle endpoint. Loads each
    /// offering through <see cref="LoadForWriteAsync"/> (re-using the existing
    /// scope + mutability guards), applies <paramref name="apply"/>, and
    /// commits independently so a failed peer does not roll back successes.
    /// Reason is plumbed through for future audit storage — today it's
    /// captured only in the in-process correlation/log path.
    /// </summary>
    private async Task<BulkActionResult> BulkApplyAsync(
        IReadOnlyList<Guid> ids,
        Action<CourseOfferingEntity, string?> apply,
        string? reason,
        CancellationToken cancellationToken)
    {
        var succeeded = new List<Guid>(ids.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in ids.Distinct())
        {
            try
            {
                var offering = await LoadForWriteAsync(id, cancellationToken);
                apply(offering, reason);
                offering.UpdatedAt = DateTime.UtcNow;
                _offerings.Update(offering);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            catch (InvalidOperationException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.InvalidTransition, Message = ex.Message });
            }
        }

        return BulkActionResult.From(succeeded, failures);
    }
}
