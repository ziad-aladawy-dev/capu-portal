using CapitalUniversity.Core.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StaffPermissionConfiguration : IEntityTypeConfiguration<StaffPermissionOverride>
{
    public void Configure(EntityTypeBuilder<StaffPermissionOverride> builder)
    {
        builder.ToTable("StaffPermissions");
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.Action)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasOne(sp => sp.Staff)
            .WithMany()
            .HasForeignKey(sp => sp.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sp => sp.Resource)
            .WithMany()
            .HasForeignKey(sp => sp.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
