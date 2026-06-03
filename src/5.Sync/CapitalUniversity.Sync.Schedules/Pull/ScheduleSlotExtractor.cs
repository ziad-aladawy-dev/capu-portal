using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Schedules.Configuration;
using CapitalUniversity.Sync.Schedules.Domain;
using CapitalUniversity.Sync.Schedules.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Schedules.Pull;

/// <summary>
/// ISO-8601 <c>UpdatedAt</c> cursor with a configurable clawback (see
/// <see cref="SchedulesSyncOptions.ExtractorSafetyBufferSeconds"/>).
/// </summary>
public sealed class ScheduleSlotExtractor : IDataExtractor<ExternalScheduleSlot>, ICursorObserver
{
    private readonly IExternalScheduleSlotSource _source;
    private readonly IOptions<SchedulesSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public ScheduleSlotExtractor(IExternalScheduleSlotSource source, IOptions<SchedulesSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalScheduleSlot> ExtractAsync(
        SyncContext context,
        SyncCheckpoint? checkpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var safetyBuffer = TimeSpan.FromSeconds(Math.Max(0, _options.Value.ExtractorSafetyBufferSeconds));

        DateTimeOffset? since = null;
        if (checkpoint?.Cursor is { Length: > 0 } cursor &&
            DateTimeOffset.TryParse(cursor, out var parsed))
        {
            since = parsed - safetyBuffer;
        }

        await foreach (var slot in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || slot.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = slot.ExternalUpdatedAt;
            }
            yield return slot;
        }
    }
}
