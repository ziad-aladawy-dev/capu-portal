using CapitalUniversity.Sync.Staff.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Staff.Persistence;

/// <summary>
/// Sync-side DbContext — outbox only. Operational staff rows live in Core's
/// <c>dbo.Staffs</c> and are written through <c>ICoreWriteGateway</c>.
/// </summary>
public sealed class StaffSyncDbContext : DbContext
{
    public const string SchemaName = "sync_staff";

    public StaffSyncDbContext(DbContextOptions<StaffSyncDbContext> options)
        : base(options) { }

    public DbSet<StaffOutboxEntity> StaffOutbox => Set<StaffOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StaffSyncDbContext).Assembly);
    }
}
