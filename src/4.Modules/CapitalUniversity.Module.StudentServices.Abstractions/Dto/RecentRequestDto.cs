namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class RecentRequestDto
{
    public int RequestNumber { get; set; }
    public Guid RequestId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}