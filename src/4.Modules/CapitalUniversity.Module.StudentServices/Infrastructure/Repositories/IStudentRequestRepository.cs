using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Domain;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public interface IStudentRequestRepository
{
    Task<StudentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetAllForStaffAsync(CancellationToken cancellationToken = default);
    Task AddAsync(StudentRequest request, CancellationToken cancellationToken = default);
    void Update(StudentRequest request);
    void Delete(StudentRequest request);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<RequestCountsDto> GetRequestCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<RequestCountsDto> GetRequestCountsByStatusForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default);
    Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default);
    Task<List<RecentRequestDto>> GetRecentRequestsAsync(int count, CancellationToken cancellationToken = default);
    Task<PagedResult<StaffRequestListItemDto>> GetPagedRequestsForStaffAsync(int page, int pageSize, string? search, string? sortBy, bool ascending, Guid? staffId = null, CancellationToken cancellationToken = default);

    Task<List<DailyRequestCountDto>> GetRequestTrendAsync(int days, CancellationToken cancellationToken = default);
    Task<List<RequestAttachment>> GetAttachmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StudentRequest?> GetPendingForStudentAndServiceAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default);
}