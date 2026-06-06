using System.Collections.Concurrent;
using CapitalUniversity.Sync.Finance.Domain;

namespace CapitalUniversity.Sync.Finance.Sources;

/// <summary>
/// Verification-only in-memory sink — mirror of <c>InMemoryExternalStudentSink</c>.
/// Idempotency dedup on the supplied key keeps a SaveChanges-after-push crash
/// from producing a duplicate side effect.
/// </summary>
public sealed class InMemoryExternalInvoiceSink : IExternalInvoiceSink
{
    private readonly ConcurrentDictionary<string, ExternalInvoice> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _seenIdempotencyKeys =
        new(StringComparer.Ordinal);

    private int _pushInvocationCount;

    public IReadOnlyDictionary<string, ExternalInvoice> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    public int PushInvocationCount => Volatile.Read(ref _pushInvocationCount);

    public void FailNextPushFor(string externalInvoiceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalInvoiceId);
        _armedFailures[externalInvoiceId] = 0;
    }

    public void ClearAccepted()
    {
        _accepted.Clear();
        _seenIdempotencyKeys.Clear();
    }

    public Task PushAsync(ExternalInvoice payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _pushInvocationCount);

        if (_seenIdempotencyKeys.ContainsKey(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        if (_armedFailures.TryRemove(payload.ExternalInvoiceId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalInvoiceSink: armed failure for ExternalInvoiceId={payload.ExternalInvoiceId}.");
        }

        _accepted[payload.ExternalInvoiceId] = payload;
        _seenIdempotencyKeys[idempotencyKey] = 0;
        return Task.CompletedTask;
    }
}
