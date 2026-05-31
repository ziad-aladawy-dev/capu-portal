using System.Collections.Concurrent;
using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

public sealed class InMemoryExternalStaffSink : IExternalStaffSink
{
    private readonly ConcurrentDictionary<string, ExternalStaff> _accepted =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte> _armedFailures =
        new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ExternalStaff> Accepted => _accepted;

    public int AcceptedCount => _accepted.Count;

    public void FailNextPushFor(string externalStaffId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalStaffId);
        _armedFailures[externalStaffId] = 0;
    }

    public void ClearAccepted() => _accepted.Clear();

    public Task PushAsync(ExternalStaff payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (_armedFailures.TryRemove(payload.ExternalStaffId, out _))
        {
            throw new InvalidOperationException(
                $"InMemoryExternalStaffSink: armed failure for ExternalStaffId={payload.ExternalStaffId}.");
        }

        _accepted[payload.ExternalStaffId] = payload;
        return Task.CompletedTask;
    }
}