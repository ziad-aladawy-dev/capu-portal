using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;

public class StudentServicesDbContext : DbContext
{
    public StudentServicesDbContext(DbContextOptions<StudentServicesDbContext> options) : base(options) { }

    public DbSet<Service> Services { get; set; }
    public DbSet<ServiceStructureNode> ServiceStructureNodes { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowStepField> WorkflowStepFields { get; set; }
    public DbSet<StudentRequest> StudentRequests { get; set; }
    public DbSet<RequestHistoryEntry> RequestHistoryEntries { get; set; }
    public DbSet<RequestAttachment> RequestAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore StructureNode entity (already mapped in CoreDbContext)
        modelBuilder.Entity<StructureNode>(entity =>
        {
            entity.ToTable("StructureNodes", "dbo", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.Name);
            entity.Ignore(e => e.Type);
            entity.Ignore(e => e.ParentId);
            entity.Ignore(e => e.Order);
            entity.Ignore(e => e.Path);
            entity.Ignore(e => e.Depth);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.Parent);
            entity.Ignore(e => e.Children);
        });

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentServicesDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Prevent modification of auto-generated RequestNumber
        foreach (var entry in ChangeTracker.Entries<StudentRequest>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.RequestNumber).IsModified = false;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}