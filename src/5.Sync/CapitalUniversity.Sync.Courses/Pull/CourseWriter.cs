using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Courses.Pull;

/// <summary>
/// Sync.Courses writer — thin adapter over <see cref="ICoreWriteGateway"/>.
/// Inserts allowed (Core's Course has no required cross-entity FK), so sync
/// IS the source-of-truth for new catalog entries on this module.
/// </summary>
public sealed class CourseWriter : IRecordWriter<Course>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<CourseWriter> _logger;

    public CourseWriter(ICoreWriteGateway gateway, ILogger<CourseWriter> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<Course> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var result = await _gateway.UpsertAsync<Course>(
            batch,
            applyUpdate: (existing, incoming) =>
            {
                existing.Code = incoming.Code;
                existing.Title = incoming.Title;
                existing.CreditHours = incoming.CreditHours;
                existing.Category = incoming.Category;
                existing.IsActive = incoming.IsActive;
                // IsClosed / ClosedAt are lifecycle fields managed by Core; sync
                // doesn't reopen a closed course.
            },
            new CoreUpsertOptions { AllowInsert = true, RespectExternalUpdatedAt = true },
            cancellationToken).ConfigureAwait(false);

        if (result.SkippedNotNewer > 0)
        {
            _logger.LogDebug(
                "Sync.Courses: {SkippedNotNewer} stale upstream replays skipped (external-wins guard).",
                result.SkippedNotNewer);
        }

        return result.Persisted;
    }
}
