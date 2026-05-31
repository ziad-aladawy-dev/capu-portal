using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Staff.Persistence;

public sealed class StaffSyncDbContext : DbContext
{
    public const string SchemaName = "sync_staff";

    public StaffSyncDbContext(DbContextOptions<StaffSyncDbContext> options)
        : base(options) { }

    public DbSet<StaffEntity> Staff => Set<StaffEntity>();
    public DbSet<StaffOutboxEntity> StaffOutbox => Set<StaffOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StaffSyncDbContext).Assembly);
    }
}