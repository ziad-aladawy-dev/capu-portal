using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Configuration;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Staff.Pull;

/// <summary>
/// Mirrors <c>StudentExtractor</c>: ISO-8601 <c>UpdatedAt</c> cursor with a
/// configurable clawback (see <see cref="StaffSyncOptions.ExtractorSafetyBufferSeconds"/>).
/// </summary>
public sealed class StaffExtractor : IDataExtractor<ExternalStaff>, ICursorObserver
{
    private readonly IExternalStaffSource _source;
    private readonly IOptions<StaffSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    /// <inheritdoc />
    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public StaffExtractor(IExternalStaffSource source, IOptions<StaffSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalStaff> ExtractAsync(
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

        await foreach (var staff in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || staff.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = staff.ExternalUpdatedAt;
            }
            yield return staff;
        }
    }
}