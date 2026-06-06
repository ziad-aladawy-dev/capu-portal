using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Courses.Persistence;
using CapitalUniversity.Sync.Courses.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Courses.Push;

/// <summary>
/// Push sink for the Courses Internal → External flow. Mirror of
/// <see cref="Staff.Push.StaffOutboxWriter"/>.
/// </summary>
public sealed class CourseOutboxWriter : IRecordWriter<CourseOutboxDispatch>
{
    private const int LastErrorMaxLength = 4000;

    private readonly CoursesSyncDbContext _db;
    private readonly IExternalCourseSink _sink;
    private readonly ILogger<CourseOutboxWriter> _logger;

    public CourseOutboxWriter(
        CoursesSyncDbContext db,
        IExternalCourseSink sink,
        ILogger<CourseOutboxWriter> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<CourseOutboxDispatch> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var processed = 0;

        foreach (var dispatch in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dispatch.Row.Status != OutboxStatus.Pending)
            {
                continue;
            }

            try
            {
                // Idempotency key is the outbox row's stable Guid — see
                // IExternalCourseSink for the at-least-once contract.
                await _sink.PushAsync(
                    dispatch.Payload,
                    dispatch.Row.Id.ToString(),
                    cancellationToken).ConfigureAwait(false);

                dispatch.Row.Status = OutboxStatus.Processed;
                dispatch.Row.ProcessedAt = DateTimeOffset.UtcNow;
                dispatch.Row.LastError = null;
                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                dispatch.Row.AttemptCount += 1;
                dispatch.Row.LastError = TextHelpers.Truncate(ex.Message, LastErrorMaxLength);

                if (dispatch.Row.AttemptCount >= CourseOutboxEntity.MaxAttempts)
                {
                    dispatch.Row.Status = OutboxStatus.Failed;
                    _logger.LogError(ex,
                        "Outbox row poisoned after {Attempts} attempts. ExternalCourseId={Id} LastError={Error}. Status=Failed; manual intervention required.",
                        dispatch.Row.AttemptCount,
                        dispatch.Row.ExternalCourseId,
                        ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Push sink failed. ExternalCourseId={Id} AttemptCount={Attempt}/{MaxAttempts} Error={Error}.",
                        dispatch.Row.ExternalCourseId,
                        dispatch.Row.AttemptCount,
                        CourseOutboxEntity.MaxAttempts,
                        ex.Message);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }
}
