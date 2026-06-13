using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

namespace CapitalUniversity.Module.StudentServices.Application;

public class DashboardStatisticsService : IDashboardStatisticsService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IStudentRequestRepository _requestRepository;
    private readonly ILocalizationService _localization;

    public DashboardStatisticsService(
        IServiceRepository serviceRepository,
        IStudentRequestRepository requestRepository,
        ILocalizationService localization)
    {
        _serviceRepository = serviceRepository;
        _requestRepository = requestRepository;
        _localization = localization;
    }

    public async Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _requestRepository.GetStaffStatisticsAsync(cancellationToken);
    }

    public async Task<StudentStatisticsDto> GetStudentStatisticsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var counts = await _requestRepository.GetRequestCountsByStatusForStudentAsync(studentId, cancellationToken);
        return new StudentStatisticsDto { RequestsByStatus = counts };
    }

    public async Task<List<DailyRequestCountDto>> GetRequestTrendAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        return await _requestRepository.GetRequestTrendAsync(days, cancellationToken);
    }

    public async Task<List<RecentRequestDto>> GetRecentRequestsAsync(int count = 5, CancellationToken cancellationToken = default)
    {
        var recent = await _requestRepository.GetRecentRequestsAsync(count, cancellationToken);
        foreach (var item in recent)
        {
            item.StudentName = _localization.Get<string>(item.StudentName);
            item.ServiceName = _localization.Get<string>(item.ServiceName);
        }
        return recent;
    }
}