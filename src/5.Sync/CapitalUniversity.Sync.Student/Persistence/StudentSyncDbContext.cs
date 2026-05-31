using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Student.Persistence;

public sealed class StudentSyncDbContext : DbContext
{
    public const string SchemaName = "sync_student";

    public StudentSyncDbContext(DbContextOptions<StudentSyncDbContext> options)
        : base(options) { }

    public DbSet<StudentEntity> Students => Set<StudentEntity>();

    public DbSet<StudentOutboxEntity> StudentOutbox => Set<StudentOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentSyncDbContext).Assembly);
    }
}