using System.Collections.Concurrent;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

/// <summary>
/// Verification-only in-memory sink — mirror of <c>InMemoryExternalStudentSink</c>.
/// Idempotency dedup on the supplied key (HTTP <c>Idempotency-Key</c> semantics)
/// keeps a SaveChanges-after-push crash from producing a duplicate side effect.
/// </summary>
public sealed class InMemoryExternalStaffSink : IExternalStaffSink
{
    private readonly ConcurrentDictionary<string, ExternalStaff> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _seenIdempotencyKeys =
        new(StringComparer.Ordinal);

    private int _pushInvocationCount;

    public IReadOnlyDictionary<string, ExternalStaff> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    /// <summary>
    /// Total invocations of <see cref="PushAsync"/> — including ones that
    /// short-circuited via idempotency-key dedup. See
    /// <c>InMemoryExternalStudentSink</c> for the full rationale.
    /// </summary>
    public int PushInvocationCount => Volatile.Read(ref _pushInvocationCount);

    public void FailNextPushFor(string externalStaffId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStaffId);
        _armedFailures[externalStaffId] = 0;
    }

    public void ClearAccepted()
    {
        _accepted.Clear();
        _seenIdempotencyKeys.Clear();
    }

    public Task PushAsync(ExternalStaff payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _pushInvocationCount);

        if (_seenIdempotencyKeys.ContainsKey(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        if (_armedFailures.TryRemove(payload.ExternalStaffId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalStaffSink: armed failure for ExternalStaffId={payload.ExternalStaffId}.");
        }

        _accepted[payload.ExternalStaffId] = payload;
        _seenIdempotencyKeys[idempotencyKey] = 0;
        return Task.CompletedTask;
    }
}
