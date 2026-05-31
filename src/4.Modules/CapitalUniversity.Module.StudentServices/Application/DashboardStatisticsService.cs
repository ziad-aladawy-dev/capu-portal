using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

namespace CapitalUniversity.Module.StudentServices.Application;

public class DashboardStatisticsService : IDashboardStatisticsService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IStudentRequestRepository _requestRepository;

    public DashboardStatisticsService(
        IServiceRepository serviceRepository,
        IStudentRequestRepository requestRepository)
    {
        _serviceRepository = serviceRepository;
        _requestRepository = requestRepository;
    }

    public async Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var allServices = await _serviceRepository.GetAllActiveAsync(cancellationToken);
        var totalServices = allServices.Count;
        var activeServices = allServices.Count(s => s.IsActive);

        var requestsByStatus = await _requestRepository.GetRequestCountsByStatusAsync(cancellationToken);
        var totalRevenue = await _requestRepository.GetTotalRevenueAsync(cancellationToken);

        return new StaffStatisticsDto
        {
            TotalServices = totalServices,
            ActiveServices = activeServices,
            RequestsByStatus = requestsByStatus,
            TotalRevenue = totalRevenue
        };
    }

    public async Task<StudentStatisticsDto> GetStudentStatisticsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var requestsByStatus = await _requestRepository.GetRequestCountsByStatusForStudentAsync(studentId, cancellationToken);
        return new StudentStatisticsDto
        {
            RequestsByStatus = requestsByStatus
        };
    }
}