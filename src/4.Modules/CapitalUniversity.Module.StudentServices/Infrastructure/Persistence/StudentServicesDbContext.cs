using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;

public class StudentServicesDbContext : DbContext
{
    public StudentServicesDbContext(DbContextOptions<StudentServicesDbContext> options) : base(options) { }

    public DbSet<Service> Services { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowStepAction> WorkflowStepActions { get; set; }
    public DbSet<StudentRequest> StudentRequests { get; set; }
    public DbSet<RequestHistoryEntry> RequestHistoryEntries { get; set; }
    public DbSet<RequestAttachment> RequestAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentServicesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}