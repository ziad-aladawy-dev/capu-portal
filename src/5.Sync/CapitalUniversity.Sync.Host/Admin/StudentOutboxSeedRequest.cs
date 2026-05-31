namespace CapitalUniversity.Sync.Host.Admin;

/// <summary>
/// Body for <c>POST /admin/outbox/student/{externalStudentId}</c>. Every field is
/// optional — omitted fields fall back to canned defaults so a bare curl-without-body
/// still seeds a valid Pending row.
/// </summary>
public sealed class StudentOutboxSeedRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset? ExternalUpdatedAt { get; set; }
    public int? ExternalVersion { get; set; }
}