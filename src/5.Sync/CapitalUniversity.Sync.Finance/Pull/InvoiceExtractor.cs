using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Finance.Configuration;
using CapitalUniversity.Sync.Finance.Domain;
using CapitalUniversity.Sync.Finance.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Finance.Pull;

/// <summary>
/// ISO-8601 <c>UpdatedAt</c> cursor with a configurable clawback. Finance
/// defaults to a wider clawback (see <see cref="FinanceSyncOptions.ExtractorSafetyBufferSeconds"/>)
/// because bursar systems frequently back-date adjustments after end-of-day
/// reconciliation.
/// </summary>
public sealed class InvoiceExtractor : IDataExtractor<ExternalInvoice>, ICursorObserver
{
    private readonly IExternalInvoiceSource _source;
    private readonly IOptions<FinanceSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public InvoiceExtractor(IExternalInvoiceSource source, IOptions<FinanceSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalInvoice> ExtractAsync(
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

        await foreach (var inv in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || inv.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = inv.ExternalUpdatedAt;
            }
            yield return inv;
        }
    }
}
