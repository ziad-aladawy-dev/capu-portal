using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Users;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StaffPermissionConfiguration : IEntityTypeConfiguration<StaffPermissionOverride>
{
    public void Configure(EntityTypeBuilder<StaffPermissionOverride> builder)
    {
        builder.ToTable("StaffPermissions");
        builder.HasKey(sp => sp.Id);

        builder.HasOne(sp => sp.Staff)
            .WithMany()
            .HasForeignKey(sp => sp.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sp => sp.Service)
            .WithMany()
            .HasForeignKey(sp => sp.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}