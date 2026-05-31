using System.Collections.Concurrent;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Sources;

/// <summary>
/// Verification-only in-memory sink. Stores the latest accepted payload per
/// <see cref="ExternalStudent.ExternalStudentId"/> so admin endpoints / tests can
/// inspect what would have been sent. Replaced by a real HTTP client in production.
///
/// <para>
/// <see cref="FailNextPushFor(string)"/> arms a one-shot failure for the next
/// <see cref="PushAsync"/> call against the given external id — used by runtime
/// verification to exercise the outbox AttemptCount / LastError path without
/// touching production code.
/// </para>
/// </summary>
public sealed class InMemoryExternalStudentSink : IExternalStudentSink
{
    private readonly ConcurrentDictionary<string, ExternalStudent> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ExternalStudent> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    public void FailNextPushFor(string externalStudentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStudentId);
        _armedFailures[externalStudentId] = 0;
    }

    public void ClearAccepted() => _accepted.Clear();

    public Task PushAsync(ExternalStudent payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (_armedFailures.TryRemove(payload.ExternalStudentId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalStudentSink: armed failure for ExternalStudentId={payload.ExternalStudentId}.");
        }

        _accepted[payload.ExternalStudentId] = payload;
        return Task.CompletedTask;
    }
}