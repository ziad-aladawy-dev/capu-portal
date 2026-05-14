namespace CapitalUniversity.Core.Abstractions.Shared;

public class UserStatisticsDto
{
    public int TotalStudents { get; set; }

    public int ActiveStudents { get; set; }

    public int InactiveStudents { get; set; }

    public int TotalStaff { get; set; }

    public int ActiveStaff { get; set; }

    public int InactiveStaff { get; set; }
}