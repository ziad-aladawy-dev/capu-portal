namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/student/{externalStudentId}</c>. Every field is
/// optional — omitted fields fall back to canned defaults so a bare
/// curl-without-body still seeds a valid Pending row. Field names match the
/// Core <c>Identity.Student</c> shape.
/// </summary>
public sealed class StudentOutboxSeedRequest
{
    public string? StudentCode { get; set; }
    public string? Name { get; set; }
    public string? NationalId { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? ExternalStructureNodeKey { get; set; }
    public bool? IsActive { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}
