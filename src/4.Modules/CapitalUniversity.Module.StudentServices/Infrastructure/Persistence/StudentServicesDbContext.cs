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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentServicesDbContext).Assembly);

        // StructureNode is owned by Core (dbo.StructureNodes). It is reachable in
        // this context only through ServiceStructureNode.StructureNode, which the
        // service-scope query reads for path matching. Map it onto the existing
        // Core table and exclude it from this context's schema script — otherwise
        // EF invents an empty dbo.StructureNode table and the scope FK targets it,
        // so seeding/scoping inserts (which use real Core node ids) fail.
        modelBuilder.Entity<StructureNode>(b =>
            b.ToTable("StructureNodes", "dbo", t => t.ExcludeFromMigrations()));

        base.OnModelCreating(modelBuilder);
    }
}