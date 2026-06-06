using CapitalUniversity.Sync.Staff.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Staff.Persistence.Configurations;

internal sealed class StaffOutboxConfiguration : IEntityTypeConfiguration<StaffOutboxEntity>
{
    public void Configure(EntityTypeBuilder<StaffOutboxEntity> builder)
    {
        builder.ToTable("staff_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalStaffId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.PayloadSchemaVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_staff_outbox_Status_CreatedAt");
    }
}