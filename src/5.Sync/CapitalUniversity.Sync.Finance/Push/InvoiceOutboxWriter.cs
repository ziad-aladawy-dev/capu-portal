using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Finance.Persistence;
using CapitalUniversity.Sync.Finance.Sources;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Finance.Push;

/// <summary>
/// Push sink for the Invoice Internal → External flow. Mirror of
/// <see cref="Staff.Push.StaffOutboxWriter"/>.
/// </summary>
public sealed class InvoiceOutboxWriter : IRecordWriter<InvoiceOutboxDispatch>
{
    private const int LastErrorMaxLength = 4000;

    private readonly FinanceSyncDbContext _db;
    private readonly IExternalInvoiceSink _sink;
    private readonly ILogger<InvoiceOutboxWriter> _logger;

    public InvoiceOutboxWriter(
        FinanceSyncDbContext db,
        IExternalInvoiceSink sink,
        ILogger<InvoiceOutboxWriter> logger)
    {
        _db = db;
        _sink = sink;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<InvoiceOutboxDispatch> batch,
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
                // IExternalInvoiceSink for the at-least-once contract.
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

                if (dispatch.Row.AttemptCount >= InvoiceOutboxEntity.MaxAttempts)
                {
                    dispatch.Row.Status = OutboxStatus.Failed;
                    _logger.LogError(ex,
                        "Outbox row poisoned after {Attempts} attempts. ExternalInvoiceId={Id} LastError={Error}.",
                        dispatch.Row.AttemptCount,
                        dispatch.Row.ExternalInvoiceId,
                        ex.Message);
                }
                else
                {
                    _logger.LogWarning(ex,
                        "Push sink failed. ExternalInvoiceId={Id} AttemptCount={Attempt}/{MaxAttempts} Error={Error}.",
                        dispatch.Row.ExternalInvoiceId,
                        dispatch.Row.AttemptCount,
                        InvoiceOutboxEntity.MaxAttempts,
                        ex.Message);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }
}
