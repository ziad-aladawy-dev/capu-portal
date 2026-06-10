namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class StaffStatisticsDto
{
    public int TotalServices { get; set; }
    public int ActiveServices { get; set; }
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public int AwaitingApproval { get; set; }
    public int CompletedRequests { get; set; }
    public int PaidRequests { get; set; }
    public decimal TotalRevenue { get; set; }
}