using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly StudentServicesDbContext _context;

    public ServiceRepository(StudentServicesDbContext context)
    {
        _context = context;
    }

    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services
            .Include(s => s.ScopeNodes)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Service?> GetByIdWithWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services
            .Include(s => s.ScopeNodes)
            .Include(s => s.Workflow)
                .ThenInclude(w => w.Steps)
                    .ThenInclude(step => step.Fields)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Service?> GetByIdWithScopeAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services
            .Include(s => s.ScopeNodes)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Services
            .Where(s => s.IsActive)
            .Include(s => s.Workflow)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<Service>> GetAvailableForStudentAsync(Guid studentId, string? studentNodePath, Guid? currentAcademicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.Services
            .Where(s => s.IsActive)
            .Include(s => s.Workflow)
            .AsQueryable();

        if (currentAcademicYearId.HasValue)
            query = query.Where(s => s.AcademicYearId == null || s.AcademicYearId == currentAcademicYearId.Value);

        if (!string.IsNullOrEmpty(studentNodePath))
        {
            query = query.Where(s =>
                !s.ScopeNodes.Any() ||
                s.ScopeNodes.Any(sn =>
                    (s.IncludeDescendants && studentNodePath.StartsWith(sn.StructureNode.Path)) ||
                    (!s.IncludeDescendants && studentNodePath == sn.StructureNode.Path)
                )
            );
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
        => await _context.Services.AddAsync(service, cancellationToken);

    public void Update(Service service) => _context.Services.Update(service);
    public void Delete(Service service) => _context.Services.Remove(service);
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services.AnyAsync(s => s.Id == id, cancellationToken);
    public async Task<bool> IsServiceInUseAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AnyAsync(r => r.ServiceId == serviceId, cancellationToken);

    public async Task<bool> IsServiceInUseByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    => await _context.Services.AnyAsync(s => s.WorkflowId == workflowId, cancellationToken);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}