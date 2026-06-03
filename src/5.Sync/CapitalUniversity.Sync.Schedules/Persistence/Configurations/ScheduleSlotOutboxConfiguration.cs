using CapitalUniversity.Sync.Schedules.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Schedules.Persistence.Configurations;

internal sealed class ScheduleSlotOutboxConfiguration : IEntityTypeConfiguration<ScheduleSlotOutboxEntity>
{
    public void Configure(EntityTypeBuilder<ScheduleSlotOutboxEntity> builder)
    {
        builder.ToTable("schedule_slots_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalScheduleSlotId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.PayloadSchemaVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_schedule_slots_outbox_Status_CreatedAt");
    }
}
