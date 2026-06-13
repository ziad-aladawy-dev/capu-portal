using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class StaffRequestListItemDto
{
    public Guid Id { get; set; }
    public int RequestNumber { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public RequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public Guid? AssignedToStaffId { get; set; }
    public Guid StudentId { get; set; }
}