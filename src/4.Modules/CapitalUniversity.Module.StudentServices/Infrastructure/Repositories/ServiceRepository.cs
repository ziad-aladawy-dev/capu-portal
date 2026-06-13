using CapitalUniversity.Core.Domain.UniversityStructure;
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
            .AsNoTracking()
            .Include(s => s.ScopeNodes)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Service?> GetByIdWithWorkflowAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services
            .AsNoTracking()
            .Include(s => s.ScopeNodes)
            .Include(s => s.Workflow!)
                .ThenInclude(w => w.Steps!)
                    .ThenInclude(step => step.Fields!)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Service?> GetByIdWithScopeAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services
            .AsNoTracking()
            .Include(s => s.ScopeNodes)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Include(s => s.Workflow)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Services
            .AsNoTracking()
            .Include(s => s.ScopeNodes)
            .Include(s => s.Workflow)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<Service>> GetAvailableForStudentAsync(
        Guid studentId,
        string? studentNodePath,
        int? studentLevelNumber,
        Guid? currentAcademicYearId,
        Guid? currentSemesterId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Services.AsNoTracking().Where(s => s.IsActive);

        if (currentAcademicYearId.HasValue)
            query = query.Where(s => s.AcademicYearId == null || s.AcademicYearId == currentAcademicYearId.Value);
        if (currentSemesterId.HasValue)
            query = query.Where(s => s.SemesterId == null || s.SemesterId == currentSemesterId.Value);
        if (studentLevelNumber.HasValue)
            query = query.Where(s => s.LevelOrder == null || s.LevelOrder == studentLevelNumber.Value);

        // Push scope filtering to SQL. A service is available if it has no scope 
        // nodes (global) or if the student's node path matches one of its scope nodes.
        if (!string.IsNullOrEmpty(studentNodePath))
        {
            query = query.Where(s => !s.ScopeNodes.Any() || s.ScopeNodes.Any(sn =>
                _context.Set<StructureNode>()
                    .Where(n => n.Id == sn.StructureNodeId)
                    .Any(n => s.IncludeDescendants
                        ? studentNodePath.StartsWith(n.Path)
                        : studentNodePath == n.Path)));
        }

        return await query
            .Include(s => s.Workflow)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
        => await _context.Services.AddAsync(service, cancellationToken);

    public void Update(Service service) => _context.Services.Update(service);

    public async Task UpdateServiceScopeNodesAsync(Guid serviceId, IEnumerable<Guid> newScopeNodeIds, CancellationToken cancellationToken = default)
    {
        var currentIds = await _context.ServiceStructureNodes
            .Where(sn => sn.ServiceId == serviceId)
            .Select(sn => sn.StructureNodeId)
            .ToListAsync(cancellationToken);

        var newIds = newScopeNodeIds.Distinct().ToList();

        var toRemove = currentIds.Except(newIds).ToList();
        if (toRemove.Any())
        {
            var relationsToDelete = await _context.ServiceStructureNodes
                .Where(sn => sn.ServiceId == serviceId && toRemove.Contains(sn.StructureNodeId))
                .ToListAsync(cancellationToken);
            _context.ServiceStructureNodes.RemoveRange(relationsToDelete);
        }

        var toAdd = newIds.Except(currentIds).ToList();
        foreach (var nodeId in toAdd)
        {
            _context.ServiceStructureNodes.Add(new ServiceStructureNode
            {
                Id = Guid.NewGuid(),
                ServiceId = serviceId,
                StructureNodeId = nodeId,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public void Delete(Service service) => _context.Services.Remove(service);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Services.AsNoTracking().AnyAsync(s => s.Id == id, cancellationToken);

    public async Task<bool> IsServiceInUseAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => await _context.StudentRequests.AsNoTracking().AnyAsync(r => r.ServiceId == serviceId, cancellationToken);

    public async Task<bool> IsServiceInUseByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
        => await _context.Services.AsNoTracking().AnyAsync(s => s.WorkflowId == workflowId, cancellationToken);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}