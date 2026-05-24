using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly CoreDbContext _context;

    public WorkflowRepository(CoreDbContext context)
    {
        _context = context;
    }

    public Task<WorkflowDefinition?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<WorkflowDefinition>().AsQueryable();
        if (includeChildren)
        {
            query = query.Include(w => w.States).Include(w => w.Transitions);
        }
        return query.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public Task<WorkflowDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Set<WorkflowDefinition>()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Code == code, cancellationToken);

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<WorkflowDefinition>()
            .AsNoTracking()
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .OrderBy(w => w.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<WorkflowDefinition>().Where(w => w.Code == code);
        if (excludeId.HasValue) query = query.Where(w => w.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default) =>
        await _context.Set<WorkflowDefinition>().AddAsync(workflow, cancellationToken);

    public void Update(WorkflowDefinition workflow) => _context.Set<WorkflowDefinition>().Update(workflow);

    public void Delete(WorkflowDefinition workflow) => _context.Set<WorkflowDefinition>().Remove(workflow);
}
