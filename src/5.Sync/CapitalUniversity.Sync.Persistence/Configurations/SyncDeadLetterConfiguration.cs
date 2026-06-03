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

        // One dead-letter row per Hangfire job. The SyncDeadLetterFilter can
        // be re-applied for the same job (Hangfire's documented double-FailedState
        // artifact, plus normal retry-exhaustion races between workers). The
        // unique index — not an exists-check inside the filter — is the
        // authoritative race-stopper: a duplicate insert provokes a constraint
        // violation that the filter catches as the idempotency signal.
        builder.HasIndex(x => x.HangfireJobId)
            .IsUnique()
            .HasDatabaseName("IX_dead_letters_HangfireJobId");
    }
}