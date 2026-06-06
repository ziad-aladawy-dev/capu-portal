using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Persistence.Configurations;

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRunEntity>
{
    public void Configure(EntityTypeBuilder<SyncRunEntity> builder)
    {
        builder.ToTable("runs");
        builder.HasKey(x => x.CorrelationId);

        builder.Property(x => x.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TriggeredBy).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Queue).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Direction).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.HangfireJobId).HasMaxLength(64);

        builder.HasIndex(x => new { x.ModuleName, x.EnqueuedAt });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Status, x.HangfireJobId }).HasDatabaseName("IX_runs_orphan");
    }
}