using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Staff.Persistence;
using CapitalUniversity.Sync.Staff.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Staff.Push;

/// <summary>
/// Push sink for the Staff Internal → External flow. Mirror of
/// <see cref="Student.Push.StudentOutboxWriter"/>.
/// </summary>
public sealed class StaffOutboxWriter : IRecordWriter<StaffOutboxDispatch>
{
    private const int LastErrorMaxLength = 4000;

    private readonly StaffSyncDbContext _db;
    private readonly IExternalStaffSink _sink;
    private readonly ILogger<StaffOutboxWriter> _logger;

    public StaffOutboxWriter(
        StaffSyncDbContext db,
        IExternalStaffSink sink,
        ILogger<StaffOutboxWriter> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<StaffOutboxDispatch> batch,
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
                // IExternalStaffSink for the at-least-once contract.
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

                if (dispatch.Row.AttemptCount >= StaffOutboxEntity.MaxAttempts)
                {
                    dispatch.Row.Status = OutboxStatus.Failed;
                    _logger.LogError(ex,
                        "Outbox row poisoned after {Attempts} attempts. ExternalStaffId={Id} LastError={Error}. Status=Failed; manual intervention required.",
                        dispatch.Row.AttemptCount,
                        dispatch.Row.ExternalStaffId,
                        ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Push sink failed. ExternalStaffId={Id} AttemptCount={Attempt}/{MaxAttempts} Error={Error}.",
                        dispatch.Row.ExternalStaffId,
                        dispatch.Row.AttemptCount,
                        StaffOutboxEntity.MaxAttempts,
                        ex.Message);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }
}