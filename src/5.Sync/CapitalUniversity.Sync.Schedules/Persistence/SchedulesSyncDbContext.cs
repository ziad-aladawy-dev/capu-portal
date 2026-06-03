using CapitalUniversity.Sync.Schedules.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Schedules.Persistence;

/// <summary>
/// Sync-side DbContext — outbox only. Operational schedule slots live in
/// Core's Schedule module and are written through <c>ICoreWriteGateway</c>.
/// </summary>
public sealed class SchedulesSyncDbContext : DbContext
{
    public const string SchemaName = "sync_schedules";

    public SchedulesSyncDbContext(DbContextOptions<SchedulesSyncDbContext> options)
        : base(options) { }

    public DbSet<ScheduleSlotOutboxEntity> ScheduleSlotsOutbox => Set<ScheduleSlotOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulesSyncDbContext).Assembly);
    }
}
