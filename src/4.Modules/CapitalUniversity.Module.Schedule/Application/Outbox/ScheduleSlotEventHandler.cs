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

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
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
