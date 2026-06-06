using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Pull;

/// <summary>
/// Maps an upstream <see cref="ExternalScheduleSlot"/> into a Core
/// <see cref="ScheduleSlot"/> wrapped in <see cref="ScheduleSlotSyncDispatch"/>.
/// <para>
/// <see cref="ScheduleSlot.StartTime"/> + <see cref="ScheduleSlot.EndTime"/>
/// have private setters and the entity enforces <c>End &gt; Start</c> through
/// <see cref="ScheduleSlot.SetTimeRange"/>. The mapper creates a fresh entity
/// (which is mutable, IsClosed = false by default) and uses SetTimeRange so
/// the invariant is honored at construction time.
/// </para>
/// </summary>
public sealed class ScheduleSlotMapper : IRecordMapper<ExternalScheduleSlot, ScheduleSlotSyncDispatch>
{
    public ScheduleSlotSyncDispatch Map(ExternalScheduleSlot external)
    {
        ArgumentNullException.ThrowIfNull(external);

        var entity = new ScheduleSlot
        {
            ExternallySourced = new()
            {
                ExternalId = external.ExternalScheduleSlotId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            // CourseOfferingId left at default — writer resolves and assigns
            // before forwarding to the gateway.
            CourseOfferingId = Guid.Empty,

            DayOfWeek = external.DayOfWeek,
            Kind = external.Kind,

            // Bilingual JSON when supplied; null when upstream omits.
            Location = string.IsNullOrWhiteSpace(external.Location)
                ? null
                : LocalizedJson.Normalize(external.Location.Trim()),
            Notes = string.IsNullOrWhiteSpace(external.Notes)
                ? null
                : LocalizedJson.Normalize(external.Notes.Trim()),
        };

        // SetTimeRange validates End > Start; throws if upstream sent a
        // zero-length or inverted slot. The validator catches this case earlier
        // when possible, but the entity-level guard is the final defense.
        entity.SetTimeRange(external.StartTime, external.EndTime);

        return new ScheduleSlotSyncDispatch
        {
            Entity = entity,
            ExternalCourseOfferingId = external.ExternalCourseOfferingId.Trim()
        };
    }
}
