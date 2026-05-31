namespace CapitalUniversity.Sync.Staff.Domain;

/// <summary>
/// External shape received from the upstream University HR system. Carries
/// staff-specific identity + department alongside the standard sync metadata.
/// </summary>
public sealed class ExternalStaff
{
    public required string ExternalStaffId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Department { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}