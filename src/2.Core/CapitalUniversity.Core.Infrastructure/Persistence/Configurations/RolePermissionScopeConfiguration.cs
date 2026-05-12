using CapitalUniversity.Core.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class RolePermissionScopeConfiguration : IEntityTypeConfiguration<RolePermissionScope>
{
    public void Configure(EntityTypeBuilder<RolePermissionScope> builder)
    {
        builder.ToTable("RolePermissionScopes");
        builder.HasKey(rps => rps.Id);

        builder.HasOne(rps => rps.RolePermission)
            .WithMany(rp => rp.Scopes)
            .HasForeignKey(rps => rps.RolePermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}