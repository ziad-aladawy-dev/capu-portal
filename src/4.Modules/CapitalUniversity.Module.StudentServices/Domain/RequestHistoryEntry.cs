using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class RequestHistoryEntry : BaseEntity
{
    public Guid StudentRequestId { get; set; }
    public StudentRequest StudentRequest { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByRole { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
}