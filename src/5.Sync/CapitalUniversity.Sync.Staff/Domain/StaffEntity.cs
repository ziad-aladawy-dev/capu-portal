namespace CapitalUniversity.Sync.Staff.Domain;

/// <summary>
/// Internal staff entity. ExternalStaffId is the stable merge key per
/// Sync_Platform_Model.md. Mirrors <c>StudentEntity</c> shape with an additional
/// <c>Department</c> field — distinct domain to prove modules can carry their own
/// fields without infrastructure coupling.
/// </summary>
public sealed class StaffEntity
{
    public Guid Id { get; set; }
    public string ExternalStaffId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTimeOffset ExternalUpdatedAt { get; set; }
    public int ExternalVersion { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
    public string OriginSystem { get; set; } = "external";
}