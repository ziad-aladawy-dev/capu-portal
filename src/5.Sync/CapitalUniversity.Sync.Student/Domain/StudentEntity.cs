namespace CapitalUniversity.Sync.Student.Domain;

/// <summary>
/// Internal entity. ExternalStudentId is the stable merge key (per
/// Sync_Platform_Model.md). All sync metadata fields are present so future
/// repair/reconciliation jobs can detect drift.
/// </summary>
public sealed class StudentEntity
{
    public Guid Id { get; set; }
    public string ExternalStudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset ExternalUpdatedAt { get; set; }
    public int ExternalVersion { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
    public string OriginSystem { get; set; } = "external";
}