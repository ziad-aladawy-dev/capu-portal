using System.Collections.Concurrent;
using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Sources;

/// <summary>
/// Verification-only in-memory sink — mirror of <c>InMemoryExternalStudentSink</c>.
/// Idempotency dedup on the supplied key keeps a SaveChanges-after-push crash
/// from producing a duplicate side effect.
/// </summary>
public sealed class InMemoryExternalCourseSink : IExternalCourseSink
{
    private readonly ConcurrentDictionary<string, ExternalCourse> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _seenIdempotencyKeys =
        new(StringComparer.Ordinal);

    private int _pushInvocationCount;

    public IReadOnlyDictionary<string, ExternalCourse> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    public int PushInvocationCount => Volatile.Read(ref _pushInvocationCount);

    public void FailNextPushFor(string externalCourseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalCourseId);
        _armedFailures[externalCourseId] = 0;
    }

    public void ClearAccepted()
    {
        _accepted.Clear();
        _seenIdempotencyKeys.Clear();
    }

    public Task PushAsync(ExternalCourse payload, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _pushInvocationCount);

        if (_seenIdempotencyKeys.ContainsKey(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        if (_armedFailures.TryRemove(payload.ExternalCourseId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalCourseSink: armed failure for ExternalCourseId={payload.ExternalCourseId}.");
        }

        _accepted[payload.ExternalCourseId] = payload;
        _seenIdempotencyKeys[idempotencyKey] = 0;
        return Task.CompletedTask;
    }
}
