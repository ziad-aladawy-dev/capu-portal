using CapitalUniversity.Module.StudentServices.Abstractions.Dto;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IDashboardStatisticsService
{
    Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default);
    Task<StudentStatisticsDto> GetStudentStatisticsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<RecentRequestDto>> GetRecentRequestsAsync(int count = 5, CancellationToken cancellationToken = default);
    Task<List<DailyRequestCountDto>> GetRequestTrendAsync(int days = 30, CancellationToken cancellationToken = default);
}