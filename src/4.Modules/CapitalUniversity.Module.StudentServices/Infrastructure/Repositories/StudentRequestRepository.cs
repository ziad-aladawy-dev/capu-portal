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
            .Include(r => r.Service)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await _context.StudentRequests
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
            .Where(r => r.StudentId == studentId)
            .Include(r => r.Service)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Where(r => r.AssignedToStaffId == staffId)
            .Include(r => r.Service)
                .ThenInclude(s => s.Workflow)
                .ThenInclude(w => w.Steps)
            .OrderByDescending(r => r.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAllForStaffAsync(CancellationToken cancellationToken = default)
        => await _context.StudentRequests
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
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = groups.ToDictionary(x => x.Status, x => x.Count);
        return MapCounts(dict);
    }

    public async Task<RequestCountsDto> GetRequestCountsByStatusForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var groups = await _context.StudentRequests
            .Where(r => r.StudentId == studentId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = groups.ToDictionary(x => x.Status, x => x.Count);
        return MapCounts(dict);
    }

    public async Task<decimal> GetTotalRevenueAsync(CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
            .SumAsync(r => r.AmountPaid ?? 0, cancellationToken);

    public async Task<StaffStatisticsDto> GetStaffStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalServices = await _context.Services.CountAsync(cancellationToken);
        var activeServices = await _context.Services.CountAsync(s => s.IsActive, cancellationToken);

        var totalRequests = await _context.StudentRequests.CountAsync(cancellationToken);
        var pendingRequests = await _context.StudentRequests.CountAsync(r => r.Status == RequestStatus.Pending, cancellationToken);
        var underReviewRequests = await _context.StudentRequests.CountAsync(r => r.Status == RequestStatus.UnderReview, cancellationToken);
        var completedRequests = await _context.StudentRequests.CountAsync(r => r.Status == RequestStatus.Completed, cancellationToken);
        var paidRequests = await _context.StudentRequests.CountAsync(r => r.PaymentStatus == PaymentStatus.Paid, cancellationToken);
        var totalRevenue = await _context.StudentRequests
            .Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
            .SumAsync(r => r.AmountPaid ?? 0, cancellationToken);

        return new StaffStatisticsDto
        {
            TotalServices = totalServices,
            ActiveServices = activeServices,
            TotalRequests = totalRequests,
            PendingRequests = pendingRequests,
            AwaitingApproval = underReviewRequests,
            CompletedRequests = completedRequests,
            PaidRequests = paidRequests,
            TotalRevenue = totalRevenue
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
        var query = _context.StudentRequests.Include(r => r.Service).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search) && int.TryParse(search, out int reqNumber))
        {
            query = query.Where(r => r.RequestNumber == reqNumber);
        }

        // Assignment visibility: unassigned requests visible to all, assigned only to the assignee
        if (staffId.HasValue)
        {
            query = query.Where(r => r.AssignedToStaffId == null || r.AssignedToStaffId == staffId);
        }

        var allRequests = await query.ToListAsync(cancellationToken);
        var studentIds = allRequests.Select(r => r.StudentId).Distinct().ToList();
        var studentsDict = new Dictionary<Guid, string>();
        if (studentIds.Any())
        {
            var students = await _coreDbContext.Students
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(cancellationToken);
            studentsDict = students.ToDictionary(s => s.Id, s => s.Name);
        }

        var detailedRequests = allRequests.Select(r => new
        {
            Request = r,
            StudentName = studentsDict.GetValueOrDefault(r.StudentId) ?? string.Empty
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search) && !int.TryParse(search, out _))
        {
            var searchLower = search.ToLowerInvariant();
            detailedRequests = detailedRequests
                .Where(x => x.StudentName.ToLowerInvariant().Contains(searchLower))
                .ToList();
        }

        var totalCount = detailedRequests.Count;
        IOrderedEnumerable<dynamic> ordered;
        switch (sortBy?.ToLower())
        {
            case "requestnumber":
                ordered = ascending
                    ? detailedRequests.OrderBy(x => x.Request.RequestNumber)
                    : detailedRequests.OrderByDescending(x => x.Request.RequestNumber);
                break;
            case "studentname":
                ordered = ascending
                    ? detailedRequests.OrderBy(x => x.StudentName)
                    : detailedRequests.OrderByDescending(x => x.StudentName);
                break;
            default:
                ordered = detailedRequests.OrderByDescending(x => x.Request.CreatedAt);
                break;
        }

        var pagedList = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = pagedList.Select(x => new StaffRequestListItemDto
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
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<List<RequestAttachment>> GetAttachmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
        => await _context.RequestAttachments
            .Where(a => a.StudentRequestId == requestId)
            .ToListAsync(cancellationToken);

    public async Task<StudentRequest?> GetPendingForStudentAndServiceAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Include(r => r.Service)
                .ThenInclude(s => s.Workflow)
                    .ThenInclude(w => w.Steps)
                        .ThenInclude(s => s.Fields)
            .Include(r => r.HistoryEntries)
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