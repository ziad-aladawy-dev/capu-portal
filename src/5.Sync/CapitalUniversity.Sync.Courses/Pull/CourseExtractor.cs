using System.Runtime.CompilerServices;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Courses.Configuration;
using CapitalUniversity.Sync.Courses.Domain;
using CapitalUniversity.Sync.Courses.Sources;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Courses.Pull;

/// <summary>
/// Mirrors <c>StudentExtractor</c>: ISO-8601 <c>UpdatedAt</c> cursor with a
/// configurable clawback (see <see cref="CoursesSyncOptions.ExtractorSafetyBufferSeconds"/>).
/// </summary>
public sealed class CourseExtractor : IDataExtractor<ExternalCourse>, ICursorObserver
{
    private readonly IExternalCourseSource _source;
    private readonly IOptions<CoursesSyncOptions> _options;

    private DateTimeOffset? _maxExternalUpdatedAt;

    /// <inheritdoc />
    public string? CurrentCursor => _maxExternalUpdatedAt?.ToString("O");

    public CourseExtractor(IExternalCourseSource source, IOptions<CoursesSyncOptions> options)
    {
        _source = source;
        _options = options;
    }

    public async IAsyncEnumerable<ExternalCourse> ExtractAsync(
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

        await foreach (var course in _source
            .StreamChangesAsync(since, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_maxExternalUpdatedAt is null || course.ExternalUpdatedAt > _maxExternalUpdatedAt)
            {
                _maxExternalUpdatedAt = course.ExternalUpdatedAt;
            }
            yield return course;
        }
    }
}
