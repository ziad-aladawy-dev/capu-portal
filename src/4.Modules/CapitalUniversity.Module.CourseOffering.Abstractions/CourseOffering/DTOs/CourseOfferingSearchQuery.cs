using CapitalUniversity.Core.Abstractions.Shared.Paging;

namespace CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;

/// <summary>
/// Cross-node offering search. Every filter ANDs with the rest.
/// </summary>
/// <remarks>
/// Sort whitelist: <c>createdAt</c>, <c>sectionCode</c>, <c>status</c>.
/// Default order: <c>createdAt:desc</c>.
/// </remarks>
public class CourseOfferingSearchQuery : PagedQueryRequest
{
    public Guid? SemesterId { get; set; }
    public Guid? StructureNodeId { get; set; }
    public Guid? CourseId { get; set; }
    public OfferingStatus? Status { get; set; }
    public RegistrationState? RegistrationState { get; set; }
}
