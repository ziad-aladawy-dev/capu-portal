using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Schedules.Persistence;
using CapitalUniversity.Sync.Schedules.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Schedules.Push;

public sealed class ScheduleSlotOutboxWriter : IRecordWriter<ScheduleSlotOutboxDispatch>
{
    private const int LastErrorMaxLength = 4000;

    private readonly SchedulesSyncDbContext _db;
    private readonly IExternalScheduleSlotSink _sink;
    private readonly ILogger<ScheduleSlotOutboxWriter> _logger;

    public ScheduleSlotOutboxWriter(
        SchedulesSyncDbContext db,
        IExternalScheduleSlotSink sink,
        ILogger<ScheduleSlotOutboxWriter> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<ScheduleSlotOutboxDispatch> batch,
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
                // IExternalScheduleSlotSink for the at-least-once contract.
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

                if (dispatch.Row.AttemptCount >= ScheduleSlotOutboxEntity.MaxAttempts)
                {
                    dispatch.Row.Status = OutboxStatus.Failed;
                    _logger.LogError(ex,
                        "Outbox row poisoned after {Attempts} attempts. ExternalScheduleSlotId={Id} LastError={Error}.",
                        dispatch.Row.AttemptCount,
                        dispatch.Row.ExternalScheduleSlotId,
                        ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Push sink failed. ExternalScheduleSlotId={Id} AttemptCount={Attempt}/{MaxAttempts} Error={Error}.",
                        dispatch.Row.ExternalScheduleSlotId,
                        dispatch.Row.AttemptCount,
                        ScheduleSlotOutboxEntity.MaxAttempts,
                        ex.Message);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }
}
