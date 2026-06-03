using System.Collections.Concurrent;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Sources;

/// <summary>
/// Verification-only in-memory sink. Stores the latest accepted payload per
/// <see cref="ExternalStudent.ExternalStudentId"/> so admin endpoints / tests can
/// inspect what would have been sent. Replaced by a real HTTP client in production.
///
/// <para>
/// <b>Idempotency.</b> Implements the <see cref="IExternalStudentSink"/>
/// contract: a repeat call with the same <c>idempotencyKey</c> is a no-op
/// (returns success without recording or invoking the underlying handler).
/// Matches the standard HTTP <c>Idempotency-Key</c> semantics — and that's
/// what keeps a SaveChanges-after-push crash from causing a duplicate
/// external side effect on the next tick.
/// </para>
///
/// <para>
/// <see cref="FailNextPushFor(string)"/> arms a one-shot failure for the next
/// <see cref="PushAsync"/> call against the given external id — used by runtime
/// verification to exercise the outbox AttemptCount / LastError path without
/// touching production code. An armed failure runs BEFORE the idempotency key
/// is committed to the seen-set, mirroring "the real handler ran and threw —
/// so the next call must be retryable".
/// </para>
/// </summary>
public sealed class InMemoryExternalStudentSink : IExternalStudentSink
{
    private readonly ConcurrentDictionary<string, ExternalStudent> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _seenIdempotencyKeys =
        new(StringComparer.Ordinal);

    private int _pushInvocationCount;

    /// <summary>
    /// Latest payload accepted per external id. Re-pushes via the idempotency
    /// path do NOT update this dictionary — the value reflects the first
    /// successful invocation per key.
    /// </summary>
    public IReadOnlyDictionary<string, ExternalStudent> Accepted => _accepted;

    /// <summary>Count of distinct external ids that have been accepted.</summary>
    public int AcceptedCount => _accepted.Count;

    /// <summary>
    /// Total invocations of <see cref="PushAsync"/> — including ones that
    /// short-circuited via the idempotency-key dedup. Lets tests prove the
    /// caller actually re-attempted while the external side effect was
    /// suppressed.
    /// </summary>
    public int PushInvocationCount => Volatile.Read(ref _pushInvocationCount);

    public void FailNextPushFor(string externalStudentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStudentId);
        _armedFailures[externalStudentId] = 0;
    }

    public void ClearAccepted()
    {
        _accepted.Clear();
        _seenIdempotencyKeys.Clear();
    }

    public Task PushAsync(ExternalStudent payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _pushInvocationCount);

        // Idempotency dedup — short-circuit a repeat of an already-accepted call.
        // Same semantics as receiving the same Idempotency-Key on a real HTTP
        // sink: 200 OK from cache, the underlying handler does not run again.
        if (_seenIdempotencyKeys.ContainsKey(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        // Armed failure simulates the handler throwing — happens BEFORE we
        // commit the idempotency record so the next call can succeed.
        if (_armedFailures.TryRemove(payload.ExternalStudentId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalStudentSink: armed failure for ExternalStudentId={payload.ExternalStudentId}.");
        }

        _accepted[payload.ExternalStudentId] = payload;
        _seenIdempotencyKeys[idempotencyKey] = 0;
        return Task.CompletedTask;
    }
}
