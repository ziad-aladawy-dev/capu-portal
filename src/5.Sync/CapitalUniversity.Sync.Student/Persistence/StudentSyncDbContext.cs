using CapitalUniversity.Sync.Student.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Student.Persistence;

/// <summary>
/// Sync-side DbContext — owns ONLY the per-module outbox table now.
/// Operational student rows live in Core's <c>dbo.Students</c> and are written
/// through <c>ICoreWriteGateway</c>; sync no longer duplicates that entity.
/// </summary>
public sealed class StudentSyncDbContext : DbContext
{
    public const string SchemaName = "sync_student";

    public StudentSyncDbContext(DbContextOptions<StudentSyncDbContext> options)
        : base(options) { }

    public DbSet<StudentOutboxEntity> StudentOutbox => Set<StudentOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentSyncDbContext).Assembly);
    }
}
