namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class StudentStatisticsDto
{
    public RequestCountsDto RequestsByStatus { get; set; } = new();
}