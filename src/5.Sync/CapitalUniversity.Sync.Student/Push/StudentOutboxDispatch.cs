using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Intermediate shape produced by the push mapper. Carries the original outbox row
/// (so the writer can update Status/AttemptCount/LastError on the same tracked
/// instance) alongside the deserialized payload sent to the external sink.
/// </summary>
public sealed class StudentOutboxDispatch
{
    public required StudentOutboxEntity Row { get; init; }
    public required ExternalStudent Payload { get; init; }
}