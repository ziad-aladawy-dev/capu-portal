using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly StudentServicesDbContext _context;

    public WorkflowRepository(StudentServicesDbContext context)
    {
        _context = context;
    }

    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<Workflow?> GetByIdWithStepsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.Fields)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<List<Workflow>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Workflows
            .Include(w => w.Steps)
                .ThenInclude(s => s.Fields)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Workflow workflow, CancellationToken cancellationToken = default)
        => await _context.Workflows.AddAsync(workflow, cancellationToken);

    public void Update(Workflow workflow) => _context.Workflows.Update(workflow);
    public void Delete(Workflow workflow) => _context.Workflows.Remove(workflow);
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Workflows.AnyAsync(w => w.Id == id, cancellationToken);

    public async Task<WorkflowStep?> GetStepByIdAsync(Guid stepId, CancellationToken cancellationToken = default)
        => await _context.WorkflowSteps
            .Include(s => s.Fields)
            .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken);

    public void UpdateStep(WorkflowStep step) => _context.WorkflowSteps.Update(step);

    public void DeleteStep(WorkflowStep step) => _context.WorkflowSteps.Remove(step);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}