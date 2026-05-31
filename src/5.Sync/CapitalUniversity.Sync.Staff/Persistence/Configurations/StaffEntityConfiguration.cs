using CapitalUniversity.Sync.Staff.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Staff.Persistence.Configurations;

internal sealed class StaffEntityConfiguration : IEntityTypeConfiguration<StaffEntity>
{
    public void Configure(EntityTypeBuilder<StaffEntity> builder)
    {
        builder.ToTable("staff");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalStaffId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Department).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OriginSystem).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.ExternalStaffId).IsUnique();
        builder.HasIndex(x => x.ExternalUpdatedAt);
    }
}