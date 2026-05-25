using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public class StudentServiceRequestRepository : IStudentServiceRequestRepository
{
    private readonly CoreDbContext _context;

    public StudentServiceRequestRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<StudentServiceRequest?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<StudentServiceRequest>().AsQueryable();
        if (includeChildren)
        {
            query = query
                .Include(r => r.StudentService)
                .Include(r => r.FieldValues)
                    .ThenInclude(v => v.FieldDefinition)
                .Include(r => r.Documents)
                    .ThenInclude(d => d.DocumentDefinition);
        }
        return query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentServiceRequest>> GetByPaymentReferenceAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentServiceRequest>()
            // Tracking is intentional — callers (the InvoicePaidEventHandler)
            // mutate the returned rows and commit through IUnitOfWork.
            .Where(r => r.PaymentReferenceId == invoiceId)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<StudentServiceRequest> Items, int Total)> ListAsync(
        StudentServiceRequestListQuery query,
        IReadOnlyCollection<ServiceRequestStatus>? statusInclude,
        CancellationToken cancellationToken = default)
    {
        var q = _context.Set<StudentServiceRequest>()
            .AsNoTracking()
            .Include(r => r.StudentService)
            .AsQueryable();

        if (statusInclude is { Count: > 0 })
        {
            q = q.Where(r => statusInclude.Contains(r.CurrentStatus));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(r => r.CurrentStatus == query.Status.Value);
        }
        if (query.StudentServiceId.HasValue)
        {
            q = q.Where(r => r.StudentServiceId == query.StudentServiceId.Value);
        }
        if (query.StudentId.HasValue)
        {
            q = q.Where(r => r.StudentId == query.StudentId.Value);
        }
        if (query.AssignedStaffId.HasValue)
        {
            q = q.Where(r => r.AssignedStaffId == query.AssignedStaffId.Value);
        }
        if (query.SubmittedFrom.HasValue)
        {
            q = q.Where(r => r.SubmittedAt >= query.SubmittedFrom.Value);
        }
        if (query.SubmittedTo.HasValue)
        {
            q = q.Where(r => r.SubmittedAt <= query.SubmittedTo.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            // Search hits StudentService.Code (machine identifier) — names
            // are bilingual JSON so a naive Like on Name would false-match.
            q = q.Where(r => r.StudentService != null && EF.Functions.Like(r.StudentService.Code, $"%{s}%"));
        }

        var total = await q.CountAsync(cancellationToken);

        var ascending = query.SortAscending ?? false;
        q = (query.SortBy ?? "createdAt").ToLowerInvariant() switch
        {
            "submittedat" => ascending ? q.OrderBy(r => r.SubmittedAt) : q.OrderByDescending(r => r.SubmittedAt),
            "status"      => ascending ? q.OrderBy(r => r.CurrentStatus) : q.OrderByDescending(r => r.CurrentStatus),
            _             => ascending ? q.OrderBy(r => r.CreatedAt) : q.OrderByDescending(r => r.CreatedAt),
        };

        var items = await q
            .Skip(Math.Max(0, query.Page - 1) * Math.Max(1, query.PageSize))
            .Take(Math.Max(1, query.PageSize))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task AddAsync(StudentServiceRequest request, CancellationToken cancellationToken = default) =>
        await _context.Set<StudentServiceRequest>().AddAsync(request, cancellationToken);

    public void Update(StudentServiceRequest request) => _context.Set<StudentServiceRequest>().Update(request);

    public void Delete(StudentServiceRequest request) => _context.Set<StudentServiceRequest>().Remove(request);
}
