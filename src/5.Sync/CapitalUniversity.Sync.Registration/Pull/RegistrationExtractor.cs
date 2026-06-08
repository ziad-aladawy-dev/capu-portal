using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Registration.Configuration;
using CapitalUniversity.Sync.Registration.Domain;
using CapitalUniversity.Sync.Registration.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Registration.Pull;

/// <summary>
/// Mirrors <c>CourseExtractor</c>: an ISO-8601 <c>ExternalUpdatedAt</c> cursor
/// with a configurable clawback (see
/// <see cref="RegistrationSyncOptions.ExtractorSafetyBufferSeconds"/>) so a row
/// updated in the same second as the last checkpoint is not missed.
/// </summary>
public sealed class RegistrationExtractor : IDataExtractor<ExternalRegistration>, ICursorObserver
{
    private readonly IExternalRegistrationSource _source;
    private readonly IOptions<RegistrationSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    /// <inheritdoc />
    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public RegistrationExtractor(IExternalRegistrationSource source, IOptions<RegistrationSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalRegistration> ExtractAsync(
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

        await foreach (var registration in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || registration.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = registration.ExternalUpdatedAt;
            }
            yield return registration;
        }
    }
}
