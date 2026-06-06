using CapitalUniversity.Sync.Courses.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Courses.Persistence;

/// <summary>
/// Sync-side DbContext — outbox only. Operational course rows live in Core's
/// <c>dbo.Courses</c> and are written through <c>ICoreWriteGateway</c>.
/// </summary>
public sealed class CoursesSyncDbContext : DbContext
{
    public const string SchemaName = "sync_courses";

    public CoursesSyncDbContext(DbContextOptions<CoursesSyncDbContext> options)
        : base(options) { }

    public DbSet<CourseOutboxEntity> CoursesOutbox => Set<CourseOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoursesSyncDbContext).Assembly);
    }
}
