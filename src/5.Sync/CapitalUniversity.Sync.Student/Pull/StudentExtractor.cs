using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Configuration;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Student.Pull;

/// <summary>
/// Translates the supplied checkpoint into a server-side filter against the external
/// source. Tracks the maximum <see cref="ExternalStudent.ExternalUpdatedAt"/> seen
/// during the run; surfaced via <see cref="ICursorObserver.CurrentCursor"/> for the
/// module to persist after a successful run.
///
/// <para>
/// <b>Safety buffer (clawback).</b> The filter is applied at
/// <c>checkpoint.Cursor - <see cref="StudentSyncOptions.ExtractorSafetyBufferSeconds"/></c>,
/// not the raw cursor. This catches the back-dating scenario where the upstream
/// system records updates with a timestamp slightly before "now" (clock drift,
/// retroactive edits, transactional commit-order vs. timestamp-order mismatches).
/// Without the buffer, records arriving late would be skipped permanently once the
/// cursor advanced past their stamp; the writer's <c>ExternalStudentId</c>-keyed
/// upsert makes the resulting tiny replay safe.
/// </para>
/// </summary>
public sealed class StudentExtractor : IDataExtractor<ExternalStudent>, ICursorObserver
{
    private readonly IExternalStudentSource _source;
    private readonly IOptions<StudentSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    /// <inheritdoc />
    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public StudentExtractor(IExternalStudentSource source, IOptions<StudentSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalStudent> ExtractAsync(
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

        await foreach (var student in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || student.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = student.ExternalUpdatedAt;
            }
            yield return student;
        }
    }
}