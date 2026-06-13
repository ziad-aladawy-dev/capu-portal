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
            .AsSplitQuery()
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

    public async Task<List<StaffRequestListItemDto>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var requests = await _context.StudentRequests
            .AsNoTracking()
            .Where(r => r.AssignedToStaffId == staffId)
            .OrderByDescending(r => r.AssignedAt)
            .Select(r => new
            {
                r.Id,
                r.RequestNumber,
                r.StudentId,
                ServiceName = r.Service.Name,
                r.Status,
                r.CreatedAt,
                r.PaymentStatus,
                r.AssignedToStaffId
            })
            .ToListAsync(cancellationToken);

        if (!requests.Any()) return new List<StaffRequestListItemDto>();

        var studentIds = requests.Select(r => r.StudentId).Distinct().ToList();
        var studentsDict = await _coreDbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.StudentCode })
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.StudentCode }, cancellationToken);

        return requests.Select(r => new StaffRequestListItemDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            StudentName = studentsDict.GetValueOrDefault(r.StudentId)?.Name ?? string.Empty,
            StudentCode = studentsDict.GetValueOrDefault(r.StudentId)?.StudentCode ?? string.Empty,
            ServiceName = r.ServiceName,
            Status = r.Status,
            SubmittedAt = r.CreatedAt,
            PaymentStatus = r.PaymentStatus,
            AssignedToStaffId = r.AssignedToStaffId,
            StudentId = r.StudentId
        }).ToList();
    }

    public async Task<List<StaffRequestListItemDto>> GetAllForStaffAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _context.StudentRequests
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.RequestNumber,
                r.StudentId,
                ServiceName = r.Service.Name,
                r.Status,
                r.CreatedAt,
                r.PaymentStatus,
                r.AssignedToStaffId
            })
            .ToListAsync(cancellationToken);

        if (!requests.Any()) return new List<StaffRequestListItemDto>();

        var studentIds = requests.Select(r => r.StudentId).Distinct().ToList();
        var studentsDict = await _coreDbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.StudentCode })
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.StudentCode }, cancellationToken);

        return requests.Select(r => new StaffRequestListItemDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            StudentName = studentsDict.GetValueOrDefault(r.StudentId)?.Name ?? string.Empty,
            StudentCode = studentsDict.GetValueOrDefault(r.StudentId)?.StudentCode ?? string.Empty,
            ServiceName = r.ServiceName,
            Status = r.Status,
            SubmittedAt = r.CreatedAt,
            PaymentStatus = r.PaymentStatus,
            AssignedToStaffId = r.AssignedToStaffId,
            StudentId = r.StudentId
        }).ToList();
    }

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
        var serviceStats = await _context.Services
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(s => s.IsActive)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var requestStats = await _context.StudentRequests
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(r => r.Status == RequestStatus.Pending),
                UnderReview = g.Count(r => r.Status == RequestStatus.UnderReview),
                Completed = g.Count(r => r.Status == RequestStatus.Completed),
                Paid = g.Count(r => r.PaymentStatus == PaymentStatus.Paid),
                TotalRevenue = g.Where(r => r.PaymentStatus == PaymentStatus.Paid && r.AmountPaid.HasValue)
                                .Sum(r => r.AmountPaid ?? 0)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new StaffStatisticsDto
        {
            TotalServices = serviceStats?.Total ?? 0,
            ActiveServices = serviceStats?.Active ?? 0,
            TotalRequests = requestStats?.Total ?? 0,
            PendingRequests = requestStats?.Pending ?? 0,
            AwaitingApproval = requestStats?.UnderReview ?? 0,
            CompletedRequests = requestStats?.Completed ?? 0,
            PaidRequests = requestStats?.Paid ?? 0,
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
        var query = _context.StudentRequests.AsNoTracking().AsQueryable();

        if (staffId.HasValue)
        {
            query = query.Where(r => r.AssignedToStaffId == null || r.AssignedToStaffId == staffId);
        }

        if (!string.IsNullOrWhiteSpace(search) && int.TryParse(search, out int reqNumber))
        {
            query = query.Where(r => r.RequestNumber == reqNumber);
        }

        // Return ALL matching results projected (to allow in-memory security filtering as per original logic)
        var allRequests = await query
            .Select(r => new
            {
                r.Id,
                r.RequestNumber,
                r.StudentId,
                ServiceName = r.Service.Name,
                r.Status,
                r.CreatedAt,
                r.PaymentStatus,
                r.AssignedToStaffId
            })
            .ToListAsync(cancellationToken);

        if (!allRequests.Any())
            return new PagedResult<StaffRequestListItemDto> { Page = page, PageSize = pageSize, Items = new List<StaffRequestListItemDto>() };

        var studentIds = allRequests.Select(r => r.StudentId).Distinct().ToList();
        var studentsDict = await _coreDbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.StudentCode })
            .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.StudentCode }, cancellationToken);

        var result = allRequests.Select(r => new StaffRequestListItemDto
        {
            Id = r.Id,
            RequestNumber = r.RequestNumber,
            StudentName = studentsDict.GetValueOrDefault(r.StudentId)?.Name ?? string.Empty,
            StudentCode = studentsDict.GetValueOrDefault(r.StudentId)?.StudentCode ?? string.Empty,
            ServiceName = r.ServiceName,
            Status = r.Status,
            SubmittedAt = r.CreatedAt,
            PaymentStatus = r.PaymentStatus,
            AssignedToStaffId = r.AssignedToStaffId,
            StudentId = r.StudentId
        }).ToList();

        // Perform sorting and paging in memory to maintain parity with original logic (which supported in-memory student name search)
        if (!string.IsNullOrWhiteSpace(search) && !int.TryParse(search, out _))
        {
            var searchLower = search.ToLowerInvariant();
            result = result.Where(x => x.StudentName.ToLowerInvariant().Contains(searchLower) || x.StudentCode.ToLowerInvariant().Contains(searchLower)).ToList();
        }

        IEnumerable<StaffRequestListItemDto> ordered;
        switch (sortBy?.ToLower())
        {
            case "requestnumber":
                ordered = ascending ? result.OrderBy(x => x.RequestNumber) : result.OrderByDescending(x => x.RequestNumber);
                break;
            case "studentname":
                ordered = ascending ? result.OrderBy(x => x.StudentName) : result.OrderByDescending(x => x.StudentName);
                break;
            default:
                ordered = result.OrderByDescending(x => x.SubmittedAt);
                break;
        }

        var totalCount = result.Count;
        var pagedList = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<StaffRequestListItemDto>
        {
            Items = pagedList,
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