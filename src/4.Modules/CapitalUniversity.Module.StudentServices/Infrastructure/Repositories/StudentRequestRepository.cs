using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class StudentRequestRepository : IStudentRequestRepository
{
    private readonly StudentServicesDbContext _context;
    private readonly CoreDbContext _coreDbContext;

    public StudentRequestRepository(StudentServicesDbContext context, CoreDbContext coreDbContext)
    {
        _context = context;
        _coreDbContext = coreDbContext;
    }

    public async Task<StudentRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Include(r => r.Service)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _context.StudentRequests
            .AsNoTracking()
            .Include(r => r.Service)
                .ThenInclude(s => s.Workflow)
                    .ThenInclude(w => w.Steps)
                        .ThenInclude(step => step.Fields)
            .Include(r => r.HistoryEntries)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null) return null;

        var student = await _coreDbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken);

        if (student != null)
        {
            request.StudentCode = student.StudentCode;
            request.StudentNameJson = student.Name;
        }

        return request;
    }

    public async Task<List<StudentRequest>> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .Include(r => r.Service)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Where(r => r.AssignedToStaffId == staffId)
            .Include(r => r.Service)
                .ThenInclude(s => s.Workflow)
                .ThenInclude(w => w.Steps)
            .OrderByDescending(r => r.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAllForStaffAsync(CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Include(r => r.Service)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(StudentRequest request, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AddAsync(request, cancellationToken);

    public void Update(StudentRequest request)
    {
        _context.Entry(request).Property(x => x.RequestNumber).IsModified = false;
        _context.Entry(request).Property(x => x.RowVersion).IsModified = false;
        _context.StudentRequests.Update(request);
    }

    public void Delete(StudentRequest request) => _context.StudentRequests.Remove(request);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task<RequestCountsDto> GetRequestCountsByStatusAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _context.StudentRequests
            .AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = groups.ToDictionary(x => x.Status, x => x.Count);
        return MapCounts(dict);
    }

    public async Task<RequestCountsDto> GetRequestCountsByStatusForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var groups = await _context.StudentRequests
            .AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = groups.ToDictionary(x => x.Status, x => x.Count);
        return MapCounts(dict);
    }

    public async Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
            .SumAsync(r => r.AmountPaid ?? 0, cancellationToken);

    public async Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var serviceStats = await _context.Services
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalServices = g.Count(),
                ActiveServices = g.Count(s => s.IsActive)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var requestStats = await _context.StudentRequests
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalRequests = g.Count(),
                PendingRequests = g.Count(r => r.Status == RequestStatus.Pending),
                UnderReviewRequests = g.Count(r => r.Status == RequestStatus.UnderReview),
                CompletedRequests = g.Count(r => r.Status == RequestStatus.Completed),
                PaidRequests = g.Count(r => r.PaymentStatus == PaymentStatus.Paid),
                TotalRevenue = g.Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
                    .Sum(r => r.AmountPaid ?? 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new StaffStatisticsDto
        {
            TotalServices = serviceStats?.TotalServices ?? 0,
            ActiveServices = serviceStats?.ActiveServices ?? 0,
            TotalRequests = requestStats?.TotalRequests ?? 0,
            PendingRequests = requestStats?.PendingRequests ?? 0,
            AwaitingApproval = requestStats?.UnderReviewRequests ?? 0,
            CompletedRequests = requestStats?.CompletedRequests ?? 0,
            PaidRequests = requestStats?.PaidRequests ?? 0,
            TotalRevenue = requestStats?.TotalRevenue ?? 0
        };
    }

    public async Task<List<RecentRequestDto>> GetRecentRequestsAsync(int count, CancellationToken cancellationToken = default)
    {
        var recentRequests = await _context.StudentRequests
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .Select(r => new
            {
                r.Id,
                r.RequestNumber,
                r.StudentId,
                r.CreatedAt,
                r.Status,
                ServiceName = r.Service.Name
            })
            .ToListAsync(cancellationToken);

        if (!recentRequests.Any())
            return new List<RecentRequestDto>();

        var studentIds = recentRequests.Select(r => r.StudentId).Distinct().ToList();
        var students = await _coreDbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        var studentDict = students.ToDictionary(s => s.Id, s => s.Name);

        var result = recentRequests.Select(r => new RecentRequestDto
        {
            RequestId = r.Id,
            RequestNumber = r.RequestNumber,
            StudentName = studentDict.GetValueOrDefault(r.StudentId) ?? string.Empty,
            ServiceName = r.ServiceName,
            Status = r.Status.ToString(),
            SubmittedAt = r.CreatedAt
        }).ToList();

        return result;
    }

    public async Task<PagedResult<StaffRequestListItemDto>> GetPagedRequestsForStaffAsync(int page, int pageSize, string? search, string? sortBy, bool ascending, Guid? staffId = null, CancellationToken cancellationToken = default)
    {
        // Join with Students table in SQL (cross-module read-only mapping).
        var query = _context.StudentRequests
            .AsNoTracking()
            .Join(_context.Set<CapitalUniversity.Core.Domain.Identity.Student>(),
                r => r.StudentId,
                s => s.Id,
                (r, s) => new { Request = r, StudentName = s.Name })
            .AsQueryable();

        // Assignment visibility: unassigned requests visible to all, assigned only to the assignee
        if (staffId.HasValue)
        {
            query = query.Where(x => x.Request.AssignedToStaffId == null || x.Request.AssignedToStaffId == staffId.Value);
        }

        // Numeric search → filter by request number.
        int.TryParse(search, out int parsedNumber);
        var isNumericSearch = !string.IsNullOrWhiteSpace(search) && parsedNumber > 0;
        if (isNumericSearch)
            query = query.Where(x => x.Request.RequestNumber == parsedNumber);

        // Text search → filter by student name directly in SQL.
        if (!string.IsNullOrWhiteSpace(search) && !isNumericSearch)
            query = query.Where(x => x.StudentName.Contains(search));

        // Sorting — pushed to SQL for all fields, including student name.
        bool sortByStudentName = string.Equals(sortBy, "studentname", StringComparison.OrdinalIgnoreCase);

        if (sortByStudentName)
        {
            query = ascending
                ? query.OrderBy(x => x.StudentName).ThenByDescending(x => x.Request.CreatedAt)
                : query.OrderByDescending(x => x.StudentName).ThenByDescending(x => x.Request.CreatedAt);
        }
        else
        {
            query = (sortBy?.ToLower(), ascending) switch
            {
                ("requestnumber", true) => query.OrderBy(x => x.Request.RequestNumber),
                ("requestnumber", false) => query.OrderByDescending(x => x.Request.RequestNumber),
                _ => query.OrderByDescending(x => x.Request.CreatedAt)
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new PagedResult<StaffRequestListItemDto>
            {
                Items = new List<StaffRequestListItemDto>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }

        var items = await query
            .Include(x => x.Request.Service)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(x => new StaffRequestListItemDto
        {
            Id = x.Request.Id,
            RequestNumber = x.Request.RequestNumber,
            StudentName = x.StudentName,
            ServiceName = x.Request.Service.Name,
            Status = x.Request.Status,
            SubmittedAt = x.Request.CreatedAt,
            PaymentStatus = x.Request.PaymentStatus,
            AssignedToStaffId = x.Request.AssignedToStaffId,
            StudentId = x.Request.StudentId
        }).ToList();

        return new PagedResult<StaffRequestListItemDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<List<RequestAttachment>> GetAttachmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => await _context.RequestAttachments
            .AsNoTracking()
            .Where(a => a.StudentRequestId == requestId)
            .ToListAsync(cancellationToken);

    public async Task<StudentRequest?> GetPendingForStudentAndServiceAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .AsNoTracking()
            .Include(r => r.Service!)
                .ThenInclude(s => s.Workflow!)
                    .ThenInclude(w => w.Steps!)
                        .ThenInclude(s => s.Fields!)
            .Include(r => r.HistoryEntries!)
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.ServiceId == serviceId &&
                (r.Status == RequestStatus.Draft || r.Status == RequestStatus.PaymentPending), cancellationToken);

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
}
