using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Persistence.Context;

public sealed class SyncDbContext : DbContext
{
    public const string SchemaName = "sync";

    public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options) { }

    public DbSet<SyncRunEntity> Runs => Set<SyncRunEntity>();
    public DbSet<SyncCheckpointEntity> Checkpoints => Set<SyncCheckpointEntity>();
    public DbSet<SyncFailureEntity> Failures => Set<SyncFailureEntity>();
    public DbSet<SyncDeadLetterEntity> DeadLetters => Set<SyncDeadLetterEntity>();

    // sync.jobs table (created by 20260528151843_InitialCreate) is intentionally
    // not exposed as a DbSet — its content was write-only audit duplicated by
    // SyncRunEntity.HangfireJobId. The DB column stays orphaned; an operator
    // can drop it via a future migration when convenient.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SyncDbContext).Assembly);
    }
}