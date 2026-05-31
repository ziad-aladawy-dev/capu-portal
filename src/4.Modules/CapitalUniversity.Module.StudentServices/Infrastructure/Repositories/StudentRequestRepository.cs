using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class StudentRequestRepository : IStudentRequestRepository
{
    private readonly StudentServicesDbContext _context;

    public StudentRequestRepository(StudentServicesDbContext context)
    {
        _context = context;
    }

    public async Task<StudentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Include(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Include(x => x.Service)
                .ThenInclude(s => s.Workflow)
                    .ThenInclude(w => w.Steps)
                        .ThenInclude(ws => ws.AvailableActions)
            .Include(x => x.HistoryEntries)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<List<StudentRequest>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Where(x => x.StudentId == studentId)
            .Include(x => x.Service)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetByStatusAsync(RequestStatus status, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Where(x => x.Status == status)
            .Include(x => x.Service)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Where(x => x.AssignedToStaffId == staffId)
            .Include(x => x.Service)
            .OrderByDescending(x => x.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<StudentRequest>> GetPagedAsync(StudentRequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.StudentRequests
            .Include(x => x.Service)
            .Include(x => x.HistoryEntries)
            .AsQueryable();

        if (filter.StudentId.HasValue) query = query.Where(x => x.StudentId == filter.StudentId.Value);
        if (filter.ServiceId.HasValue) query = query.Where(x => x.ServiceId == filter.ServiceId.Value);
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.PaymentStatus.HasValue) query = query.Where(x => x.PaymentStatus == filter.PaymentStatus.Value);
        if (filter.AssignedToStaffId.HasValue) query = query.Where(x => x.AssignedToStaffId == filter.AssignedToStaffId.Value);
        if (filter.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(x => x.CreatedAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentRequest>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task AddAsync(StudentRequest request, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AddAsync(request, cancellationToken);

    public void Update(StudentRequest request) => _context.StudentRequests.Update(request);
    public void Delete(StudentRequest request) => _context.StudentRequests.Remove(request);

    public async Task<int> CountByServiceAndStatusAsync(Guid serviceId, RequestStatus status, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.CountAsync(x => x.ServiceId == serviceId && x.Status == status, cancellationToken);

    public async Task<RequestCountsDto> GetRequestCountsByStatusAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _context.StudentRequests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.Status, v => v.Count, cancellationToken);

        return MapCounts(counts);
    }

    public async Task<RequestCountsDto> GetRequestCountsByStatusForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var counts = await _context.StudentRequests
            .Where(r => r.StudentId == studentId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(k => k.Status, v => v.Count, cancellationToken);

        return MapCounts(counts);
    }

    private static RequestCountsDto MapCounts(Dictionary<RequestStatus, int> counts)
    {
        return new RequestCountsDto
        {
            Draft = counts.GetValueOrDefault(RequestStatus.Draft),
            Pending = counts.GetValueOrDefault(RequestStatus.Pending),
            UnderReview = counts.GetValueOrDefault(RequestStatus.UnderReview),
            MoreInfoRequired = counts.GetValueOrDefault(RequestStatus.MoreInfoRequired),
            Approved = counts.GetValueOrDefault(RequestStatus.Approved),
            Rejected = counts.GetValueOrDefault(RequestStatus.Rejected),
            PaymentPending = counts.GetValueOrDefault(RequestStatus.PaymentPending),
            Completed = counts.GetValueOrDefault(RequestStatus.Completed),
            Cancelled = counts.GetValueOrDefault(RequestStatus.Cancelled),
            ReadyForPickup = counts.GetValueOrDefault(RequestStatus.ReadyForPickup)
        };
    }

    public async Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StudentRequests
            .Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
            .SumAsync(r => r.AmountPaid ?? 0, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}