using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Application.Outbox;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.Schedule.Application;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IScheduleSlotRepository _slots;
    private readonly ICourseOfferingService _offerings;
    private readonly IValidator<CreateScheduleSlotRequest> _createValidator;
    private readonly IValidator<UpdateScheduleSlotRequest> _updateValidator;
    private readonly IOutbox _outbox;
    private readonly IAppLogger _logger;

    public ScheduleSlotService(
        IUnitOfWork unitOfWork,
        IScheduleSlotRepository slots,
        ICourseOfferingService offerings,
        IValidator<CreateScheduleSlotRequest> createValidator,
        IValidator<UpdateScheduleSlotRequest> updateValidator,
        IOutbox outbox,
        IAppLogger logger)
    {
        _unitOfWork = unitOfWork;
        _slots = slots;
        _offerings = offerings;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<ScheduleSlotResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await _slots.GetByIdAsync(id, cancellationToken);
        if (slot is null) return null;

        // Visibility = parent visibility. A returned-null offering means either
        // "doesn't exist" or "out of scope" — both should hide the slot.
        if (await _offerings.GetByIdAsync(slot.CourseOfferingId, cancellationToken) is null) return null;

        return ToResponse(slot);
    }

    public async Task<IReadOnlyList<ScheduleSlotResponse>> GetForOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default)
    {
        if (await _offerings.GetByIdAsync(courseOfferingId, cancellationToken) is null)
        {
            return Array.Empty<ScheduleSlotResponse>();
        }

        var slots = await _slots.GetForOfferingAsync(courseOfferingId, cancellationToken);
        return slots.Select(ToResponse).ToList();
    }

    public async Task<Guid> CreateAsync(CreateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        // Existence + scope check on the parent offering. Out-of-scope is
        // reported as NotFound on the offering's localization key so the
        // response is identical whether the slot or the offering is the
        // mismatch — no leak of "the offering exists, you just can't touch it".
        if (await _offerings.GetByIdAsync(request.CourseOfferingId, cancellationToken) is null)
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        // Exact-duplicate check (same tuple) stays — the DB unique index is
        // the real guard, this surfaces a friendly Conflict before SaveChanges.
        if (await _slots.ExistsAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.Schedule.DuplicateSlot);
        }

        // Half-open-interval overlap check — the new deterministic conflict
        // rule. Strict inequality ensures adjacency (10:00-11:00 + 11:00-12:00)
        // is NOT a conflict.
        if (await _slots.HasConflictAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, excludeId: null, cancellationToken))
        {
            await LogConflictAsync(request.CourseOfferingId, request.DayOfWeek, request.StartTime, request.EndTime, slotId: null, cancellationToken);
            throw new ConflictException(LocalizedKeys.Schedule.SlotConflict);
        }

        var slot = new ScheduleSlot
        {
            CourseOfferingId = request.CourseOfferingId,
            DayOfWeek = request.DayOfWeek,
            Kind = request.Kind,
            Location = request.Location,
            Notes = request.Notes,
        };
        // Funnel start/end through the entity invariant — the validator covers
        // the create payload, but the entity is the single source of truth so
        // a future code path that bypasses the validator still gets caught.
        slot.SetTimeRange(request.StartTime, request.EndTime);

        await _slots.AddAsync(slot, cancellationToken);
        await EnqueueLifecycleAsync(ScheduleSlotCreatedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
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
        if (request.Location is not null) slot.Location = request.Location;
        if (request.Notes is not null) slot.Notes = request.Notes;

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
                await LogConflictAsync(slot.CourseOfferingId, slot.DayOfWeek, slot.StartTime, slot.EndTime, slotId: slot.Id, cancellationToken);
                throw new ConflictException(LocalizedKeys.Schedule.SlotConflict);
            }
        }

        slot.UpdatedAt = DateTime.UtcNow;
        _slots.Update(slot);
        await EnqueueLifecycleAsync(ScheduleSlotUpdatedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var slot = await LoadForWriteAsync(id, cancellationToken);
        _slots.Delete(slot);
        await EnqueueLifecycleAsync(ScheduleSlotDeletedHandler.TypeKey, slot, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static ScheduleSlotResponse ToResponse(ScheduleSlot s) => new()
    {
        Id = s.Id,
        CourseOfferingId = s.CourseOfferingId,
        DayOfWeek = s.DayOfWeek,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Kind = s.Kind,
        Location = s.Location,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

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
    /// </summary>
    private Task LogConflictAsync(Guid courseOfferingId, DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end, Guid? slotId, CancellationToken cancellationToken)
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
            context: null,
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
