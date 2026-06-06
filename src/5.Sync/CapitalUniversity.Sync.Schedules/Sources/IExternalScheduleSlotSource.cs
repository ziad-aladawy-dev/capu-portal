using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Sources;

public interface IExternalScheduleSlotSource
{
    IAsyncEnumerable<ExternalScheduleSlot> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        CancellationToken cancellationToken);
}
