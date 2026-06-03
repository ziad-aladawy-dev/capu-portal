using System.Collections.Concurrent;
using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Sources;

/// <summary>
/// Verification-only in-memory sink — mirror of <c>InMemoryExternalStudentSink</c>.
/// Idempotency dedup on the supplied key keeps a SaveChanges-after-push crash
/// from producing a duplicate side effect.
/// </summary>
public sealed class InMemoryExternalScheduleSlotSink : IExternalScheduleSlotSink
{
    private readonly ConcurrentDictionary<string, ExternalScheduleSlot> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _seenIdempotencyKeys =
        new(StringComparer.Ordinal);

    private int _pushInvocationCount;

    public IReadOnlyDictionary<string, ExternalScheduleSlot> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    public int PushInvocationCount => Volatile.Read(ref _pushInvocationCount);

    public void FailNextPushFor(string externalScheduleSlotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalScheduleSlotId);
        _armedFailures[externalScheduleSlotId] = 0;
    }

    public void ClearAccepted()
    {
        _accepted.Clear();
        _seenIdempotencyKeys.Clear();
    }

    public Task PushAsync(ExternalScheduleSlot payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _pushInvocationCount);

        if (_seenIdempotencyKeys.ContainsKey(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        if (_armedFailures.TryRemove(payload.ExternalScheduleSlotId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalScheduleSlotSink: armed failure for ExternalScheduleSlotId={payload.ExternalScheduleSlotId}.");
        }

        _accepted[payload.ExternalScheduleSlotId] = payload;
        _seenIdempotencyKeys[idempotencyKey] = 0;
        return Task.CompletedTask;
    }
}
