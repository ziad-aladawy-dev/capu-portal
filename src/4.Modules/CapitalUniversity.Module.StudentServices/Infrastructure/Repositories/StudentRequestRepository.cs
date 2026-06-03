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
            .Include(r => r.Service)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<StudentRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Include(r => r.Service)
                .ThenInclude(s => s.Workflow)
                    .ThenInclude(w => w.Steps)
                        .ThenInclude(step => step.Fields)
            .Include(r => r.HistoryEntries)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

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
            .OrderByDescending(r => r.AssignedAt)
            .ToListAsync(cancellationToken);

    public async Task<List<StudentRequest>> GetAllForStaffAsync(CancellationToken cancellationToken = default)
        => await _context.StudentRequests
            .Include(r => r.Service)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(StudentRequest request, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AddAsync(request, cancellationToken);

    public void Update(StudentRequest request) => _context.StudentRequests.Update(request);
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