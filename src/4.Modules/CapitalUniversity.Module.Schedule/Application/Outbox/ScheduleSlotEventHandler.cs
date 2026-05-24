using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Modules.Schedule.Abstractions;

namespace CapitalUniversity.Modules.Schedule.Application.Outbox;

/// <summary>
/// Default audit-style handler for ScheduleSlot lifecycle events. Following the
/// existing <see cref="NotificationOutboxHandler"/> convention: one handler per
/// message-type discriminator, payload as a nested record, TypeKey const.
///
/// <para>
/// Behavior is intentionally narrow — handlers log the fact through
/// <see cref="IAppLogger"/> so the event lands in the standard log store with
/// no extra infrastructure. Integration consumers who need to do more
/// (replicate to a downstream timetable, push to a webhook, etc.) replace the
/// handler in DI; the dispatcher routes one handler per message type
/// (see <c>OutboxDispatcher.ProcessBatchAsync</c>), so replacement is a clean
/// swap.
/// </para>
///
/// <para>
/// <b>No business logic.</b> These handlers do not trigger registration
/// recalculation, billing, or scheduling algorithms — the Schedule module's
/// passive-metadata contract extends to its event sinks.
/// </para>
/// </summary>
public abstract class ScheduleSlotEventHandler : IOutboxMessageHandler
{
    protected readonly IAppLogger Logger;

    protected ScheduleSlotEventHandler(IAppLogger logger)
    {
        Logger = logger;
    }

    public abstract string MessageType { get; }

    public async Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
    {
        // outboxMessageId is unused — these handlers log only, and log
        // appenders are naturally idempotent (the duplicate line is at worst
        // benign noise). If a future implementation writes derived state, it
        // should adopt the same IdempotencyKey pattern as the notification
        // handler.
        var fact = JsonSerializer.Deserialize<ScheduleSlotFact>(payload)
            ?? throw new InvalidOperationException($"{GetType().Name}: payload deserialised to null.");

        await Logger.LogInfoAsync(
            message: $"{MessageType} slot={fact.ScheduleSlotId} offering={fact.CourseOfferingId} {fact.DayOfWeek} {fact.StartTime:HH\\:mm}-{fact.EndTime:HH\\:mm}",
            source: nameof(ScheduleSlotEventHandler),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["MessageType"]      = MessageType,
                ["ScheduleSlotId"]   = fact.ScheduleSlotId,
                ["CourseOfferingId"] = fact.CourseOfferingId,
                ["DayOfWeek"]        = fact.DayOfWeek.ToString(),
                ["StartTime"]        = fact.StartTime.ToString("HH:mm"),
                ["EndTime"]          = fact.EndTime.ToString("HH:mm"),
                ["Kind"]             = fact.Kind.ToString(),
            });
    }

    /// <summary>
    /// Minimal lifecycle payload — exactly the data needed to identify the slot
    /// and re-evaluate downstream views, nothing more. Notes / Location are
    /// intentionally omitted; a consumer that needs the full row can re-fetch
    /// by <see cref="ScheduleSlotId"/>. Keeps the row small and avoids leaking
    /// free-text into integration sinks.
    /// </summary>
    public record ScheduleSlotFact(
        Guid ScheduleSlotId,
        Guid CourseOfferingId,
        DayOfWeek DayOfWeek,
        TimeOnly StartTime,
        TimeOnly EndTime,
        ScheduleSlotKind Kind);
}

public sealed class ScheduleSlotCreatedHandler : ScheduleSlotEventHandler
{
    public const string TypeKey = "schedule.slot.created";
    public ScheduleSlotCreatedHandler(IAppLogger logger) : base(logger) { }
    public override string MessageType => TypeKey;
}

public sealed class ScheduleSlotUpdatedHandler : ScheduleSlotEventHandler
{
    public const string TypeKey = "schedule.slot.updated";
    public ScheduleSlotUpdatedHandler(IAppLogger logger) : base(logger) { }
    public override string MessageType => TypeKey;
}

public sealed class ScheduleSlotDeletedHandler : ScheduleSlotEventHandler
{
    public const string TypeKey = "schedule.slot.deleted";
    public ScheduleSlotDeletedHandler(IAppLogger logger) : base(logger) { }
    public override string MessageType => TypeKey;
}

/// <summary>
/// M2 — Cleans up orphan ScheduleSlots when a CourseOffering is deleted. The
/// CourseOffering producer side enqueues a <c>"course_offering.deleted"</c>
/// outbox row carrying the offering id; this handler deletes every slot
/// attached to that offering. Bulk-delete is one SQL statement on relational
/// providers (see <see cref="Repositories.ScheduleSlotRepository.DeleteForOfferingAsync"/>),
/// so the cleanup runs in constant time per offering regardless of slot count.
///
/// <para>
/// Idempotent by virtue of the underlying DELETE WHERE — a replay finds zero
/// matching rows and is a no-op. No <c>IdempotencyKey</c> column is needed
/// here because there is no row inserted on the consumer side.
/// </para>
/// </summary>
public sealed class CourseOfferingDeletedHandler : Core.Abstractions.CrossCutting.Outbox.IOutboxMessageHandler
{
    public const string TypeKey = "course_offering.deleted";

    private readonly Repositories.IScheduleSlotRepository _slots;
    private readonly Core.Abstractions.CrossCutting.Logging.IAppLogger _logger;

    public CourseOfferingDeletedHandler(
        Repositories.IScheduleSlotRepository slots,
        Core.Abstractions.CrossCutting.Logging.IAppLogger logger)
    {
        _slots = slots;
        _logger = logger;
    }

    public string MessageType => TypeKey;

    public async Task HandleAsync(Guid outboxMessageId, string payload, CancellationToken cancellationToken)
    {
        // outboxMessageId is unused — the underlying DELETE is naturally
        // idempotent. Keeps the H5 handler contract identical.
        var fact = System.Text.Json.JsonSerializer.Deserialize<CourseOfferingDeletedFact>(payload)
            ?? throw new InvalidOperationException($"{nameof(CourseOfferingDeletedHandler)}: payload deserialised to null.");

        var deleted = await _slots.DeleteForOfferingAsync(fact.CourseOfferingId, cancellationToken);

        await _logger.LogInfoAsync(
            message: $"{TypeKey} cleaned up {deleted} schedule slot(s) for offering {fact.CourseOfferingId}.",
            source: nameof(CourseOfferingDeletedHandler),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["MessageType"]      = TypeKey,
                ["CourseOfferingId"] = fact.CourseOfferingId,
                ["DeletedCount"]     = deleted,
            });
    }

    /// <summary>
    /// Producer-side contract. When the CourseOffering module ships a delete
    /// path, it enqueues an outbox row of this shape under
    /// <see cref="TypeKey"/> in the same transaction as the offering removal.
    /// </summary>
    public record CourseOfferingDeletedFact(Guid CourseOfferingId);
}
