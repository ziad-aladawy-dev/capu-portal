namespace CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;

public class CourseOfferingResponse
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StructureNodeId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int RegisteredCount { get; set; }
    public OfferingStatus Status { get; set; }
    public RegistrationState RegistrationState { get; set; }
    public string? ExternalSystemId { get; set; }
    public DateTime? ExternalSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCourseOfferingRequest
{
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StructureNodeId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public OfferingStatus Status { get; set; } = OfferingStatus.Draft;
    public RegistrationState RegistrationState { get; set; } = RegistrationState.Closed;
    public string? ExternalSystemId { get; set; }
}

/// <summary>
/// All fields optional — only set fields are applied. <c>RegisteredCount</c>
/// is intentionally not exposed: it moves through dedicated adjustment paths
/// (future Registration module), never via the offering update endpoint.
/// </summary>
public class UpdateCourseOfferingRequest
{
    public string? SectionCode { get; set; }
    public int? Capacity { get; set; }
    public OfferingStatus? Status { get; set; }
    public RegistrationState? RegistrationState { get; set; }
    public string? ExternalSystemId { get; set; }
    public DateTime? ExternalSyncedAt { get; set; }
}
