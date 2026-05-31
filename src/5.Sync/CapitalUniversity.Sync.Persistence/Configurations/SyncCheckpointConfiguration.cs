using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Persistence.Configurations;

internal sealed class SyncCheckpointConfiguration : IEntityTypeConfiguration<SyncCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<SyncCheckpointEntity> builder)
    {
        builder.ToTable("checkpoints");
        builder.HasKey(x => x.ModuleName);

        builder.Property(x => x.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Cursor).HasMaxLength(256);
        builder.Property(x => x.LastExternalVersion).HasMaxLength(64);
    }
}