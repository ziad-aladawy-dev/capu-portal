using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public interface IStudentRequestRepository
{
    Task<StudentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetByStatusAsync(RequestStatus status, CancellationToken cancellationToken = default);
    Task<List<StudentRequest>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<PagedResult<StudentRequest>> GetPagedAsync(StudentRequestFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(StudentRequest request, CancellationToken cancellationToken = default);
    void Update(StudentRequest request);
    void Delete(StudentRequest request);
    Task<int> CountByServiceAndStatusAsync(Guid serviceId, RequestStatus status, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<RequestCountsDto> GetRequestCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<RequestCountsDto> GetRequestCountsByStatusForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default);
}

public class StudentRequestFilter
{
    public Guid? StudentId { get; set; }
    public Guid? ServiceId { get; set; }
    public RequestStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public Guid? AssignedToStaffId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}