namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/staff/{externalStaffId}</c>. Field names match
/// the Core <c>Identity.Staff</c> shape.
/// </summary>
public sealed class StaffOutboxSeedRequest
{
    public string? EmployeeCode { get; set; }
    public string? Name { get; set; }
    public string? NationalId { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? JobTitle { get; set; }
    public string? ExternalStructureNodeKey { get; set; }
    public bool? IsActive { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}
