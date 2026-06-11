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
    public Guid? InstructorId { get; set; }
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
    public Guid? InstructorId { get; set; }
    public string? ExternalSystemId { get; set; }
}

/// <summary>
/// All fields optional — only set fields are applied. <c>RegisteredCount</c>
/// is intentionally not exposed: it moves through dedicated adjustment paths
/// (future Registration module), never via the offering update endpoint.
/// <c>InstructorId</c> follows PATCH semantics with a sentinel: omitted/null =
/// unchanged, <c>Guid.Empty</c> = clear the assignment, any other value = assign.
/// </summary>
public class UpdateCourseOfferingRequest
{
    public string? SectionCode { get; set; }
    public int? Capacity { get; set; }
    public OfferingStatus? Status { get; set; }
    public RegistrationState? RegistrationState { get; set; }
    public Guid? InstructorId { get; set; }
    public string? ExternalSystemId { get; set; }
    public DateTime? ExternalSyncedAt { get; set; }
}

/// <summary>
/// Batch section creation: generates <see cref="Count"/> sections of one
/// course under one (semester, node). Section codes are generated server-side —
/// plain letter series (A, B, C…) when <see cref="SectionPrefix"/> is empty,
/// otherwise <c>{prefix}{n}</c> (e.g. EVE-1, EVE-2) — skipping codes already
/// taken by live siblings.
/// </summary>
public class BatchCreateCourseOfferingsRequest
{
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid StructureNodeId { get; set; }
    public string? SectionPrefix { get; set; }
    public int Count { get; set; } = 1;
    public int Capacity { get; set; } = 30;
    public OfferingStatus Status { get; set; } = OfferingStatus.Draft;
    public Guid? InstructorId { get; set; }
}

/// <summary>Per-row outcome of a batch section creation (partial-success semantics).</summary>
public class BatchCreateOfferingsResult
{
    public int Succeeded => Created.Count;
    public int Failed => Failures.Count;
    public List<BatchCreatedOffering> Created { get; set; } = new();
    public List<BatchOfferingFailure> Failures { get; set; } = new();
}

public class BatchCreatedOffering
{
    public Guid Id { get; set; }
    public string SectionCode { get; set; } = string.Empty;
}

public class BatchOfferingFailure
{
    public string SectionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Aggregate counters for an admin dashboard strip. Computed over the
/// offerings the caller's scope can actually see.
/// </summary>
public class CourseOfferingStatsResponse
{
    public int Total { get; set; }
    public int DraftCount { get; set; }
    public int OpenCount { get; set; }
    public int ClosedCount { get; set; }
    public int CancelledCount { get; set; }
    public int FullCount { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalRegistered { get; set; }
}
