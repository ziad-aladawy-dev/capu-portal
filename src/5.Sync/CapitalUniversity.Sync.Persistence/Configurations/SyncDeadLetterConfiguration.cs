using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Persistence.Configurations;

internal sealed class SyncDeadLetterConfiguration : IEntityTypeConfiguration<SyncDeadLetterEntity>
{
    public void Configure(EntityTypeBuilder<SyncDeadLetterEntity> builder)
    {
        builder.ToTable("dead_letters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.HangfireJobId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Direction).HasConversion<int>();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.TerminalAt);
    }
}