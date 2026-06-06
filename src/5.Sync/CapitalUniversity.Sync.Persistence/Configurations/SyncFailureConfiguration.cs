using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Persistence.Configurations;

internal sealed class SyncFailureConfiguration : IEntityTypeConfiguration<SyncFailureEntity>
{
    public void Configure(EntityTypeBuilder<SyncFailureEntity> builder)
    {
        builder.ToTable("failures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.HangfireJobId).HasMaxLength(64);
        builder.Property(x => x.ErrorType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired();

        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.OccurredAt);
    }
}