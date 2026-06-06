using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Staff.Push;

public sealed class StaffOutboxValidator : IRecordValidator<StaffOutboxDispatch>
{
    public bool IsValid(StaffOutboxDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Payload.ExternalStaffId))
        {
            error = "Outbox payload missing ExternalStaffId.";
            return false;
        }

        if (!string.Equals(record.Row.ExternalStaffId, record.Payload.ExternalStaffId, StringComparison.Ordinal))
        {
            error = "Outbox payload ExternalStaffId mismatch.";
            return false;
        }

        error = null;
        return true;
    }
}