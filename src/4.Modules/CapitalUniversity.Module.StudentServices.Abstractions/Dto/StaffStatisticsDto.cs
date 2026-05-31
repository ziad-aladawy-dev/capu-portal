namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class StaffStatisticsDto
{
    public int TotalServices { get; set; }
    public int ActiveServices { get; set; }
    public RequestCountsDto RequestsByStatus { get; set; } = new();
    public decimal TotalRevenue { get; set; }
}
