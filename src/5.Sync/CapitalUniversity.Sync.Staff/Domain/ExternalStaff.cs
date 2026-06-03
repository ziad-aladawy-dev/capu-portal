namespace CapitalUniversity.Sync.Staff.Domain;

/// <summary>
/// External shape from the upstream HR system. Mirrors the columns Core's
/// <c>Identity.Staff</c> needs for sync's UPDATE path. StructureNode is out of
/// sync scope, so there's no StructureNodeKey on the payload either.
/// </summary>
public sealed class ExternalStaff
{
    public required string ExternalStaffId { get; init; }
    public required string EmployeeCode { get; init; }
    public required string Name { get; init; }
    public required string NationalId { get; init; }
    public required DateTime BirthDate { get; init; }
    public required string PhoneNumber { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required string JobTitle { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}
