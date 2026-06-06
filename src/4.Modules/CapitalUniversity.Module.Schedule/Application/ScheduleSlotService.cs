using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Application.Outbox;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.Schedule.Application;

/// <summary>
/// Bundle of the FluentValidation validators used by <see cref="ScheduleSlotService"/>.
/// Mirrors the <c>AcademicPlanValidators</c> pattern in core — collapses two
/// individual <see cref="IValidator{T}"/> constructor parameters into one
/// composite, keeping the service constructor under the project's 7-parameter
/// soft cap when a new collaborator (here: <see cref="IHttpContextAccessor"/>)
/// joins.
/// </summary>
public sealed record ScheduleSlotValidators(
    IValidator<CreateScheduleSlotRequest> Create,
    IValidator<UpdateScheduleSlotRequest> Update);

/// <summary>
/// Owns CRUD over <see cref="ScheduleSlot"/>. Scope is inherited from the
/// parent offering — every operation first calls
/// <see cref="ICourseOfferingService.GetByIdAsync"/>, which already filters by
/// the caller's structure-node access. A miss / out-of-scope offering surfaces
/// as <see cref="NotFoundException"/> on the slot (no existence leak — mirrors
/// the InvoiceService / AcademicPlanService / CourseOfferingService pattern).
///
/// <para>
/// Conflict policy is deterministic and local: two slots on the same
/// (offering, day) conflict iff their <c>[start, end)</c> half-open intervals
/// overlap. Adjacency (e.g. 10:00-11:00 + 11:00-12:00) is allowed by design.
/// </para>
///
/// <para>
/// Lifecycle events (Created / Updated / Deleted) are enqueued on the same
/// DbContext as the row change so they either both commit or both roll back.
/// Conflict-detected facts cannot use the outbox (the surrounding txn aborts
/// on throw) — they log synchronously via <see cref="IAppLogger"/> instead,
/// honoring the "passive, audit-friendly" event contract without bolting on
/// new infrastructure.
/// </para>
///
/// <para>
/// Deliberately narrow: no recurrence expansion, no room booking, no
/// registration logic. Each slot is one descriptive metadata row.
/// </para>
/// </summary>
public class ScheduleSlotService : IScheduleSlotService
{
    // L9 — Shared-object cache key prefix, matches the Invoice pattern. The
    // cached payload is the culture-agnostic ScheduleSlotResponse DTO; the
    // current culture is applied in Localize at read time, so a single
    // cache entry serves all callers regardless of Accept-Language.
    internal const string CacheKeyPrefix = "schedule-slot:object:";
    // Collection caches (per-offering list + search pages) keyed by a version
    // stamp; any slot mutation rotates it and orphans them all in one write —
    // mirrors the Courses / AcademicPlans modules.
    internal const string CollectionVersionKey = "schedule-slot:coll:ver";
    internal const string OfferingListKeyPrefix = "schedule-slot:offering:";
    internal const string SearchKeyPrefix = "schedule-slot:search:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    // The stamp only rotates on mutation, so it long-outlives the data TTL.
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IScheduleSlotRepository _slots;
    private readonly ICourseOfferingService _offerings;
    private readonly ScheduleSlotValidators _validators;
    private readonly IOutbox _outbox;
    private readonly IAppLogger _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localization;
    private readonly ICacheService _cache;

    public ScheduleSlotService(
        IUnitOfWork unitOfWork,
        IScheduleSlotRepository slots,
        ICourseOfferingService offerings,
        ScheduleSlotValidators validators,
        IOutbox outbox,
        IAppLogger logger,
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localization,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _slots = slots;
        _offerings = offerings;
        _validators = validators;
        _outbox = outbox;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _localization = localization;
        _cache = cache;
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    public async Task<ScheduleSlotResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // L9 — shared-object cache hit path, mirrors the Invoice pattern. The
        // cached DTO carries the bilingual JSON for Location / Notes; Localize
        // applies the request's culture on read so a single cache entry serves
        // every Accept-Language. The parent-offering scope check still runs
        // on every call so a revoked grant cannot serve stale data.
        // Stampede-protected shared-object read-through. The scope-neutral DTO is
        // cached; the parent-offering scope check still runs per read so a revoked
        // grant cannot serve stale data. Not-found is not cached.
        var raw = await _cache.GetOrSetAsync<ScheduleSlotResponse>(
            CacheKey(id),
            async ct =>
            {
                var slot = await _slots.GetByIdAsync(id, ct);
                return slot is null ? null : MapToResponse(slot);
            },
            CacheTtl,
            cancellationToken);

        if (raw is null) return null;

        // Visibility = parent visibility. A returned-null offering means either
        // "doesn't exist" or "out of scope" — both should hide the slot.
        if (await _offerings.GetByIdAsync(raw.CourseOfferingId, cancellationToken) is null) return null;

        return LocalizeCopy(raw);
    }

    public async Task<IReadOnlyList<ScheduleSlotResponse>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        // Per-user scope gate stays OUT of the cache (caller-specific).
        if (await _offerings.GetByIdAsync(courseOfferingId, cancellationToken) is null)
        {
            return Array.Empty<ScheduleSlotResponse>();
        }

        // All slots under one (visible) offering — same for every caller that can
        // see the offering, so cached scope-neutral under the collection version.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<ScheduleSlotResponse>>(
            $"{OfferingListKeyPrefix}{courseOfferingId:N}:{version}",
            async ct => (await _slots.GetForOfferingAsync(courseOfferingId, ct)).Select(MapToResponse).ToList(),
            CacheTtl,
            cancellationToken);

        return (cached ?? new List<ScheduleSlotResponse>()).Select(LocalizeCopy).ToList();
    }

    public async Task<Guid> CreateAsync(CreateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Create.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        // Existence + scope check on the parent offering. Out-of-scope is
        // reported as NotFound on the offering's localization key so the
        // response is identical whether the slot or the offering is the
        // mismatch — no leak of "the offering exists, you just can't touch it".
        if (await _offerings.GetByIdAsync(request.CourseOfferingId, cancellationToken) is null)
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        // M1 — close the TOCTOU window between conflict-check and insert.
        // Without a SERIALIZABLE wrapper, two concurrent CreateAsync calls
        // against the same (offering, day) can each pass the half-open
        // overlap query and then both SaveChanges. The unique index on
        // (CourseOfferingId, DayOfWeek, StartTime, EndTime) only catches
        // EXACT duplicates; an overlapping-but-not-identical pair slips
        // through. Serialising the read+write block forces the second writer
        // to block on the range lock until the first commits, so its
        // conflict re-check sees the in-flight row.
        var newId = Guid.Empty;
        await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
        {
            // Exact-duplicate check (same tuple) stays — the DB unique index
            // is the real guard, this surfaces a friendly Conflict before
            // SaveChanges.
            if (await _slots.ExistsAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, ct))
            {
                throw new ConflictException(LocalizedKeys.Schedule.DuplicateSlot);
            }

            // Half-open-interval overlap check — the deterministic conflict
            // rule. Strict inequality keeps adjacency (10:00-11:00 + 11:00-
            // 12:00) out of the conflict set by design.
            if (await _slots.HasConflictAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, excludeId: null, ct))
            {
                await LogConflictAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, slotId: null);
                throw new ConflictException(LocalizedKeys.Schedule.SlotConflict);
            }

            var slot = new ScheduleSlot
            {
                CourseOfferingId = request.CourseOfferingId,
                DayOfWeek = request.DayOfWeek,
                Kind = request.Kind,
                Location = LocalizedJson.Normalize(request.Location),
                Notes = LocalizedJson.Normalize(request.Notes),
            };
            // Funnel start/end through the entity invariant — the validator
            // covers the create payload, but the entity is the single source
            // of truth so a future code path that bypasses the validator
            // still gets caught.
            slot.SetTimeRange(request.StartTime, request.EndTime);

            await _slots.AddAsync(slot, ct);
            await EnqueueLifecycleAsync(ScheduleSlotCreatedHandler.TypeKey, slot, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            newId = slot.Id;
        }, cancellationToken);
        // A new slot must surface in offering-list / search reads.
        await BumpCollectionVersionAsync(cancellationToken);
        return newId;
    }

    public async Task UpdateAsync(Guid id, UpdateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Update.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var slot = await LoadForWriteAsync(id, cancellationToken);

        // Snapshot the unique-index tuple before mutating so we can tell after
        // the fact whether the row's identity in that index actually moved —
        // a no-op edit on (day, start, end) does not need duplicate / overlap
        // checks (and the overlap check on an unchanged row would self-match
        // unless excluded).
        var originalDay = slot.DayOfWeek;
        var originalStart = slot.StartTime;
        var originalEnd = slot.EndTime;

        // Compose the new (start, end) pair from the request and the persisted
        // values, then apply through the entity invariant in one atomic call.
        // A partial payload that would make the slot zero/negative-length
        // surfaces as InvalidOperationException → ConflictException.
        if (request.StartTime.HasValue || request.EndTime.HasValue)
        {
            var nextStart = request.StartTime ?? slot.StartTime;
            var nextEnd = request.EndTime ?? slot.EndTime;
            try
            {
                slot.SetTimeRange(nextStart, nextEnd);
            }
            catch (InvalidOperationException)
            {
                throw new ConflictException(LocalizedKeys.Schedule.EndBeforeStart);
            }
        }

        if (request.DayOfWeek.HasValue) slot.DayOfWeek = request.DayOfWeek.Value;
        if (request.Kind.HasValue) slot.Kind = request.Kind.Value;
        if (request.Location is not null) slot.Location = LocalizedJson.Normalize(request.Location);
        if (request.Notes is not null) slot.Notes = LocalizedJson.Normalize(request.Notes);

        var tupleMoved = slot.DayOfWeek != originalDay
                      || slot.StartTime != originalStart
                      || slot.EndTime != originalEnd;
        if (tupleMoved)
        {
            if (await _slots.ExistsAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, cancellationToken))
            {
                throw new ConflictException(LocalizedKeys.Schedule.DuplicateSlot);
            }

            // Overlap check excludes this row's id so a tuple move that does
            // not collide with any *other* slot is allowed. The duplicate
            // check above is stricter (exact same tuple) and runs first.
            if (await _slots.HasConflictAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, excludeId: slot.Id, cancellationToken))
            {
                await LogConflictAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, slotId: slot.Id);
                throw new ConflictException(LocalizedKeys.Schedule.SlotConflict);
            }
        }

        slot.UpdatedAt = DateTime.UtcNow;
        _slots.Update(slot);
        await EnqueueLifecycleAsync(ScheduleSlotUpdatedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // L9 — evict the cached DTO so the next read picks up the new fields.
        await _cache.RemoveAsync(CacheKey(slot.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await LoadForWriteAsync(id, cancellationToken);
        _slots.Delete(slot);
        await EnqueueLifecycleAsync(ScheduleSlotDeletedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(slot.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task<PagedResult<ScheduleSlotResponse>> SearchAsync(ScheduleSlotSearchQuery query, CancellationToken cancellationToken = default)
    {
        // If the caller pinned an offering, scope-check via the offering
        // service (returns empty page if invisible) before hitting the slots
        // table. Cross-offering searches require the caller to be route-gated.
        if (query.CourseOfferingId.HasValue &&
            await _offerings.GetByIdAsync(query.CourseOfferingId.Value, cancellationToken) is null)
        {
            return new PagedResult<ScheduleSlotResponse>
            {
                Items = new List<ScheduleSlotResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0,
                TotalPages = 0,
            };
        }

        // Cache the scope-NEUTRAL page under the collection version + query
        // signature; per-row visibility filtering runs on read so each caller
        // sees only permitted rows while the DB query is shared.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var page = await _cache.GetOrSetAsync<PagedResult<ScheduleSlotResponse>>(
            $"{SearchKeyPrefix}{version}:{BuildSearchSignature(query)}",
            async ct =>
            {
                var result = await _slots.SearchAsync(query, ct);
                return new PagedResult<ScheduleSlotResponse>
                {
                    Items = result.Items.Select(MapToResponse).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                    TotalPages = result.TotalPages,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? new PagedResult<ScheduleSlotResponse>
            {
                Items = new List<ScheduleSlotResponse>(),
                Page = query.NormalizedPage,
                PageSize = query.NormalizedPageSize,
            };

        // For cross-offering queries, filter the materialized page by per-row
        // parent visibility. Total stays as the raw count — admins typically
        // have unrestricted scope.
        var visible = new List<ScheduleSlotResponse>(page.Items.Count);
        foreach (var s in page.Items)
        {
            if (!query.CourseOfferingId.HasValue
                && await _offerings.GetByIdAsync(s.CourseOfferingId, cancellationToken) is null) continue;
            visible.Add(LocalizeCopy(s));
        }

        return new PagedResult<ScheduleSlotResponse>
        {
            Items = visible,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            TotalPages = page.TotalPages,
        };
    }

    public async Task<BulkActionResult> BatchCreateAsync(BatchCreateScheduleSlotsRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.Slots is null || request.Slots.Count == 0)
        {
            throw new ValidationException("Slots", LocalizedKeys.Infrastructure.Required);
        }

        // Parent visibility is a request-level concern (the whole batch targets
        // one offering), so a missing / out-of-scope parent rejects the entire
        // request with NotFound — same contract as single-row CreateAsync. Only
        // the per-slot outcomes are partial.
        if (await _offerings.GetByIdAsync(request.CourseOfferingId, cancellationToken) is null)
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        var succeeded = new List<Guid>(request.Slots.Count);
        var failures = new List<BulkActionFailure>();

        // Partial-success model (matches Payments / CourseOffering bulk ops):
        // each slot is validated and committed independently so a conflicting
        // or invalid peer is reported as a per-row failure instead of rolling
        // back the whole batch. Because each success commits before the next
        // slot's conflict check runs, an intra-batch overlap surfaces as a
        // Conflict on the later slot (first-writer-wins) — no separate O(n²)
        // pre-check needed.
        foreach (var item in request.Slots)
        {
            var perSlot = new CreateScheduleSlotRequest
            {
                CourseOfferingId = request.CourseOfferingId,
                DayOfWeek = item.DayOfWeek,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                Kind = item.Kind,
                Location = item.Location,
                Notes = item.Notes,
            };

            // Built up-front so a failure can be reported against a stable id
            // (BaseEntity assigns Id at construction).
            ScheduleSlot? slot = null;
            try
            {
                var validation = await _validators.Create.ValidateAsync(perSlot, cancellationToken);
                if (!validation.IsValid) throw ValidationFrom(validation);

                slot = new ScheduleSlot
                {
                    CourseOfferingId = perSlot.CourseOfferingId,
                    DayOfWeek = perSlot.DayOfWeek,
                    Kind = perSlot.Kind,
                    Location = LocalizedJson.Normalize(perSlot.Location),
                    Notes = LocalizedJson.Normalize(perSlot.Notes),
                };
                slot.SetTimeRange(perSlot.StartTime, perSlot.EndTime);

                // Per-slot SERIALIZABLE transaction — preserves CreateAsync's M1
                // TOCTOU guard while committing each slot on its own.
                await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
                {
                    if (await _slots.ExistsAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, ct))
                    {
                        throw new ConflictException(LocalizedKeys.Schedule.DuplicateSlot);
                    }

                    if (await _slots.HasConflictAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, excludeId: null, ct))
                    {
                        await LogConflictAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, slotId: null);
                        throw new ConflictException(LocalizedKeys.Schedule.SlotConflict);
                    }

                    await _slots.AddAsync(slot, ct);
                    await EnqueueLifecycleAsync(ScheduleSlotCreatedHandler.TypeKey, slot, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, cancellationToken);

                succeeded.Add(slot.Id);
            }
            catch (ValidationException ex)
            {
                failures.Add(new BulkActionFailure { Id = slot?.Id ?? Guid.Empty, Code = BulkFailureCodes.Validation, Message = DescribeValidation(ex) });
            }
            catch (ConflictException ex)
            {
                failures.Add(new BulkActionFailure { Id = slot?.Id ?? Guid.Empty, Code = BulkFailureCodes.Conflict, Message = ex.Message });
            }
        }

        if (succeeded.Count > 0)
        {
            await BumpCollectionVersionAsync(cancellationToken);
        }

        return BulkActionResult.From(succeeded, failures);
    }

    /// <summary>
    /// Flatten a <see cref="ValidationException"/>'s per-field errors into a
    /// single message for the bulk failure row. Falls back to the exception's
    /// generic message when no field errors are present.
    /// </summary>
    private static string DescribeValidation(ValidationException ex)
    {
        var detail = string.Join("; ", ex.Errors.SelectMany(kvp => kvp.Value));
        return string.IsNullOrEmpty(detail) ? ex.Message : detail;
    }

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await LoadForWriteAsync(id, cancellationToken);
        slot.Close();
        _slots.Update(slot);
        await EnqueueLifecycleAsync(ScheduleSlotUpdatedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(slot.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await LoadForWriteAsync(id, cancellationToken);
        slot.Reopen();
        _slots.Update(slot);
        await EnqueueLifecycleAsync(ScheduleSlotUpdatedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(slot.Id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    /// <summary>
    /// Map the domain entity to its response shape, preserving any bilingual
    /// JSON strings in their raw form. Localization happens as a separate
    /// projection step (see <see cref="Localize"/>) so the raw DTO can safely
    /// be cached without poisoning across cultures.
    /// </summary>
    private static ScheduleSlotResponse MapToResponse(ScheduleSlot s) => new()
    {
        Id = s.Id,
        CourseOfferingId = s.CourseOfferingId,
        DayOfWeek = s.DayOfWeek,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Kind = s.Kind,
        Location = s.Location, // Raw JSON string
        Notes    = s.Notes,    // Raw JSON string
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    /// <summary>
    /// Decode bilingual JSON fields against the current culture. 
    /// </summary>
    private ScheduleSlotResponse Localize(ScheduleSlotResponse response)
    {
        response.Location = response.Location is null ? null : _localization.Get<string>(response.Location);
        response.Notes    = response.Notes    is null ? null : _localization.Get<string>(response.Notes);
        return response;
    }

    /// <summary>
    /// Localize onto a NEW response so cached entries (possibly the same instance
    /// under the in-memory provider) are never mutated in place.
    /// </summary>
    private ScheduleSlotResponse LocalizeCopy(ScheduleSlotResponse s) => new()
    {
        Id = s.Id,
        CourseOfferingId = s.CourseOfferingId,
        DayOfWeek = s.DayOfWeek,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Kind = s.Kind,
        Location = s.Location is null ? null : _localization.Get<string>(s.Location),
        Notes = s.Notes is null ? null : _localization.Get<string>(s.Notes),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
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

    private static string BuildSearchSignature(ScheduleSlotSearchQuery q) =>
        string.Join('|',
            $"off={q.CourseOfferingId?.ToString("N") ?? string.Empty}",
            $"day={(q.DayOfWeek.HasValue ? ((int)q.DayOfWeek.Value).ToString() : string.Empty)}",
            $"kind={(q.Kind.HasValue ? ((int)q.Kind.Value).ToString() : string.Empty)}",
            $"from={q.From?.ToString("HH:mm") ?? string.Empty}",
            $"to={q.To?.ToString("HH:mm") ?? string.Empty}",
            $"q={q.Search ?? string.Empty}",
            $"sort={q.Sort ?? string.Empty}",
            $"p={q.NormalizedPage}",
            $"ps={q.NormalizedPageSize}");

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    /// <summary>
    /// Fetch a tracked slot by id, require its parent offering to be visible
    /// to the caller. Both miss and out-of-scope map to the same NotFound on
    /// the slot's localization key, matching the existence-leak rule the rest
    /// of the project follows.
    /// </summary>
    private async Task<ScheduleSlot> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var slot = await _slots.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.Schedule.NotFound);

        if (await _offerings.GetByIdAsync(slot.CourseOfferingId, cancellationToken) is null)
        {
            throw new NotFoundException(LocalizedKeys.Schedule.NotFound);
        }

        slot.EnsureMutable();

        return slot;
    }

    /// <summary>
    /// Stage a lifecycle fact on the current DbContext so it commits in the
    /// same transaction as the slot change — outbox-pattern guarantee that
    /// successful business write ⇔ deliverable event.
    /// </summary>
    private Task EnqueueLifecycleAsync(string messageType, ScheduleSlot slot, CancellationToken cancellationToken) =>
        _outbox.EnqueueAsync(
            messageType,
            new ScheduleSlotEventHandler.ScheduleSlotFact(
                slot.Id,
                slot.CourseOfferingId,
                slot.DayOfWeek,
                slot.StartTime,
                slot.EndTime,
                slot.Kind),
            cancellationToken);

    /// <summary>
    /// Log a conflict-detected fact synchronously. The outbox is unavailable
    /// here — the caller is about to throw, which aborts the surrounding
    /// transaction and would roll back any staged outbox row. A log line is
    /// the right shape for "rejected attempt" facts: audit-friendly,
    /// observable, no infrastructure required.
    ///
    /// <para>
    /// Conflict logs always originate from an HTTP request flow (the slot
    /// create / update controllers), so we hand the current <see cref="HttpContext"/>
    /// to <see cref="IAppLogger"/> via <see cref="IHttpContextAccessor"/>. That
    /// lets the buffered logger stamp the audit row with the request's
    /// CorrelationId, keeping Mongo audit entries joinable to the Serilog
    /// text logs for the same request. When called outside a request flow
    /// (theoretical — there are no such callers today), the accessor returns
    /// null and the entry simply lacks a correlation id, matching the
    /// existing background-task behavior.
    /// </para>
    /// </summary>
    private Task LogConflictAsync(Guid courseOfferingId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, Guid? slotId)
    {
        var metadata = new Dictionary<string, object>
        {
            ["MessageType"]      = ScheduleConflictDetectedMessageType,
            ["CourseOfferingId"] = courseOfferingId,
            ["DayOfWeek"]        = dayOfWeek.ToString(),
            ["StartTime"]        = start.ToString("HH:mm"),
            ["EndTime"]          = end.ToString("HH:mm"),
        };
        if (slotId.HasValue) metadata["ScheduleSlotId"] = slotId.Value;

        return _logger.LogWarningAsync(
            message: $"{ScheduleConflictDetectedMessageType} offering={courseOfferingId} {dayOfWeek} {start:HH\\:mm}-{end:HH\\:mm}",
            source: nameof(ScheduleSlotService),
            context: _httpContextAccessor.HttpContext,
            metadata: metadata);
    }

    /// <summary>
    /// Discriminator string for conflict facts. Exposed as a constant so tests
    /// and downstream observers (log queries, metric aggregators) can pin a
    /// stable value — matches the <c>{noun}.{verb}</c> convention used by the
    /// lifecycle event TypeKeys.
    /// </summary>
    public const string ScheduleConflictDetectedMessageType = "schedule.slot.conflict_detected";
}
