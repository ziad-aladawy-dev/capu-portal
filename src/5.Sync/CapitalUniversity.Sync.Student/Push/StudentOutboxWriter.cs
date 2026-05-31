using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Student.Persistence;
using CapitalUniversity.Sync.Student.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Push sink for the Internal → External flow. For every dispatched row:
///   1. Calls <see cref="IExternalStudentSink.PushAsync"/>.
///   2. On success: marks the outbox row <see cref="OutboxStatus.Processed"/>
///      with <c>ProcessedAt = UtcNow</c>.
///   3. On failure: bumps <see cref="StudentOutboxEntity.AttemptCount"/>, captures
///      <c>LastError</c> truncated to the column width, leaves <c>Status = Pending</c>
///      so the next push tick re-attempts. Once <c>AttemptCount</c> crosses
///      <see cref="StudentOutboxEntity.MaxAttempts"/> the row is poisoned to
///      <see cref="OutboxStatus.Failed"/>.
/// One <c>SaveChangesAsync</c> per batch.
///
/// <para>
/// <b>Partial-batch isolation.</b> A single failed push does not abort the batch —
/// other dispatches still go through. This matches Hangfire's whole-execution retry
/// model.
/// </para>
/// </summary>
public sealed class StudentOutboxWriter : IRecordWriter<StudentOutboxDispatch>
{
    private const int LastErrorMaxLength = 4000;

    private readonly StudentSyncDbContext _db;
    private readonly IExternalStudentSink _sink;
    private readonly ILogger<StudentOutboxWriter> _logger;

    public StudentOutboxWriter(
        StudentSyncDbContext db,
        IExternalStudentSink sink,
        ILogger<StudentOutboxWriter> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<StudentOutboxDispatch> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var dispatch in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (dispatch.Row.Status != OutboxStatus.Pending)
            {
                // Replay defense: row was already processed in a prior attempt the
                // pipeline didn't yet flush. Skip the sink call.
                continue;
            }

            try
            {
                await _sink.PushAsync(dispatch.Payload, cancellationToken).ConfigureAwait(false);

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

                if (dispatch.Row.AttemptCount >= StudentOutboxEntity.MaxAttempts)
                {
                    dispatch.Row.Status = OutboxStatus.Failed;
                    _logger.LogError(ex,
                        "Outbox row poisoned after {Attempts} attempts. ExternalStudentId={Id} LastError={Error}. Status=Failed; manual intervention required.",
                        dispatch.Row.AttemptCount,
                        dispatch.Row.ExternalStudentId,
                        ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Push sink failed. ExternalStudentId={Id} AttemptCount={Attempt}/{MaxAttempts} Error={Error}.",
                        dispatch.Row.ExternalStudentId,
                        dispatch.Row.AttemptCount,
                        StudentOutboxEntity.MaxAttempts,
                        ex.Message);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return processed;
    }
}