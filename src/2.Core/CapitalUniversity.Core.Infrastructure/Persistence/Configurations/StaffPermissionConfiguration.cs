using CapitalUniversity.Core.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

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

        // Mirrors the StaffRoles index strategy: composite covering the per-user
        // scope filter, plus a StructureNodePath index for the prefix scan.
        builder.HasIndex(sp => new { sp.StaffId, sp.Year, sp.Semester })
            .HasDatabaseName("IX_StaffPermissions_StaffId_Year_Semester");

        builder.HasIndex(sp => sp.StructureNodePath)
            .HasDatabaseName("IX_StaffPermissions_StructureNodePath");
    }
}