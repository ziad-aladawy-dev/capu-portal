namespace CapitalUniversity.Sync.Student.Domain;

/// <summary>
/// External-system shape. Represents one student row as received from the upstream
/// University System. In production this would be deserialized from a REST API,
/// SOAP, or a direct DB read against the external warehouse.
/// </summary>
public sealed class ExternalStudent
{
    public required string ExternalStudentId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}