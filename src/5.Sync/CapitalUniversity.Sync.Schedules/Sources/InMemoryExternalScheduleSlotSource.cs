using System.Runtime.CompilerServices;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Sources;

/// <summary>
/// Deterministic in-memory simulator. Generates <see cref="TotalSlots"/> rows
/// matching the field set of Core <c>Modules.Schedule.Domain.ScheduleSlot</c>.
/// Two rows (#6 and #14) ship with EndTime &lt;= StartTime so the validator
/// drops them and warning aggregation can be observed.
/// </summary>
public sealed class InMemoryExternalScheduleSlotSource : IExternalScheduleSlotSource
{
    public const int TotalSlots = 35;

    private static readonly DateTimeOffset BaselineUpdatedAt =
        new DateTimeOffset(2026, 05, 01, 00, 00, 00, TimeSpan.Zero);

    private static readonly ScheduleSlotKind[] KindCycle =
    {
        ScheduleSlotKind.Lecture,
        ScheduleSlotKind.Lab,
        ScheduleSlotKind.Tutorial,
        ScheduleSlotKind.Seminar,
        ScheduleSlotKind.Lecture
    };

    private readonly IReadOnlyList<ExternalScheduleSlot> _store;

    public InMemoryExternalScheduleSlotSource()
    {
        var list = new List<ExternalScheduleSlot>(TotalSlots);
        for (var i = 1; i <= TotalSlots; i++)
        {
            var brokenWindow = i == 6 || i == 14;
            var startHour = 8 + (i % 9); // 08..16
            var endHour = brokenWindow ? startHour : startHour + 1;

            list.Add(new ExternalScheduleSlot
            {
                ExternalScheduleSlotId = $"EXT-SCH-{i:D5}",
                ExternalCourseOfferingId = $"EXT-CO-{((i - 1) % 20) + 1:D4}",
                DayOfWeek = (DayOfWeek)((i % 5) + 1),       // Monday..Friday
                StartTime = new TimeOnly(startHour, 0),
                EndTime = new TimeOnly(endHour, 0),
                Kind = KindCycle[i % KindCycle.Length],
                Location = $"Room {((char)('A' + (i % 5)))}-{100 + (i * 3) % 200}",
                Notes = i % 4 == 0 ? $"Recurring session {i}" : null,
                ExternalUpdatedAt = BaselineUpdatedAt.AddMinutes(i),
                ExternalVersion = 1
            });
        }
        _store = list;
    }

    public async IAsyncEnumerable<ExternalScheduleSlot> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var slot in _store)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sinceExclusive is null || slot.ExternalUpdatedAt > sinceExclusive.Value)
            {
                yield return slot;
                await Task.Yield();
            }
        }
    }
}
