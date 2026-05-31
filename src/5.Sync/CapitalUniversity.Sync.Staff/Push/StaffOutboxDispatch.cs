using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Push;

public sealed class StaffOutboxDispatch
{
    public required StaffOutboxEntity Row { get; init; }
    public required ExternalStaff Payload { get; init; }
}