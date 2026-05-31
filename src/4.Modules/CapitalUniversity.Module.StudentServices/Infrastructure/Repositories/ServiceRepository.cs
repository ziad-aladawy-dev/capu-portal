using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class ServiceRepository : IServiceRepository
{
    private readonly StudentServicesDbContext _context;
    private readonly IStructureNodeRepository _structureNodeRepository;

    public ServiceRepository(StudentServicesDbContext context, IStructureNodeRepository structureNodeRepository)
    {
        _context = context;
        _structureNodeRepository = structureNodeRepository;
    }

    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Services
            .Include(x => x.Workflow)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Service?> GetByIdWithWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Services
            .Include(x => x.Workflow)
                .ThenInclude(w => w.Steps)
                    .ThenInclude(s => s.AvailableActions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Services
            .Where(x => x.IsActive)
            .Include(x => x.Workflow)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Service>> GetAvailableForStudentAsync(Guid studentId, string? studentNodePath, string? year, string? semester, CancellationToken cancellationToken = default)
    {
        var query = _context.Services
            .Where(x => x.IsActive)
            .Include(x => x.Workflow)
            .AsQueryable();

        if (!string.IsNullOrEmpty(studentNodePath))
        {
            query = query.Where(x => x.Scope.IsGlobalStructural ||
                (x.Scope.StructureNodeId.HasValue && x.Scope.StructureNodePath != null &&
                 studentNodePath.StartsWith(x.Scope.StructureNodePath) && x.Scope.IncludeDescendants) ||
                (x.Scope.StructureNodeId.HasValue && x.Scope.StructureNodePath == studentNodePath));
        }

        if (!string.IsNullOrEmpty(year) && !string.IsNullOrEmpty(semester))
        {
            query = query.Where(x => x.Scope.IsGlobalTemporal ||
                (x.Scope.Year == year && x.Scope.Semester == semester));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        await _context.Services.AddAsync(service, cancellationToken);
    }

    public void Update(Service service) => _context.Services.Update(service);
    public void Delete(Service service) => _context.Services.Remove(service);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services.AnyAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> IsServiceInUseAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AnyAsync(x => x.ServiceId == serviceId, cancellationToken);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}