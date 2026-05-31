using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Student.Push;

/// <summary>
/// Minimal sanity gate for outbox dispatches before the external sink is called.
/// Real validation (email format, etc.) already ran on the inbound (Pull) side —
/// the outbox row's payload is authoritative for what the external system should
/// receive. We only guard against an outbox row whose payload-derived merge key
/// no longer matches the row's recorded <c>ExternalStudentId</c>, which would
/// indicate corruption between write and read.
/// </summary>
public sealed class StudentOutboxValidator : IRecordValidator<StudentOutboxDispatch>
{
    public bool IsValid(StudentOutboxDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Payload.ExternalStudentId))
        {
            error = "Outbox payload missing ExternalStudentId.";
            return false;
        }

        if (!string.Equals(
                record.Row.ExternalStudentId,
                record.Payload.ExternalStudentId,
                StringComparison.Ordinal))
        {
            error = "Outbox payload ExternalStudentId mismatch.";
            return false;
        }

        error = null;
        return true;
    }
}