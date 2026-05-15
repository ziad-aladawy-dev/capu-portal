using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StaffPermissionScopeConfiguration : IEntityTypeConfiguration<StaffPermissionScope>
{
    public void Configure(EntityTypeBuilder<StaffPermissionScope> builder)
    {
        builder.ToTable("StaffPermissionScopes");
        builder.HasKey(sps => sps.Id);

        builder.HasOne(sps => sps.StaffPermission)
            .WithMany(sp => sp.Scopes)
            .HasForeignKey(sps => sps.StaffPermissionId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}