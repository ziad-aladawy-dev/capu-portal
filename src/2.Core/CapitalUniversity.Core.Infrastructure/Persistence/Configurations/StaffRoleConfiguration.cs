using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations
{
    public class StaffRoleConfiguration : IEntityTypeConfiguration<StaffRoleAssignment>
    {
        public void Configure(EntityTypeBuilder<StaffRoleAssignment> builder)
        {
            builder.ToTable("StaffRoles");
            builder.HasKey(sr => sr.Id);

            builder.HasOne<Staff>()
                .WithMany()
                .HasForeignKey(sr => sr.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Role>()
                .WithMany()
                .HasForeignKey(sr => sr.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Hot path: PermissionService.GetPermissionsAsync filters by StaffId
            // first, then by Year/Semester equality, then by StructureNodePath
            // StartsWith. Composite index covers the (StaffId, Year, Semester)
            // selectivity; the StructureNodePath index supports the prefix scan.
            builder.HasIndex(sr => new { sr.StaffId, sr.Year, sr.Semester })
                .HasDatabaseName("IX_StaffRoles_StaffId_Year_Semester");

            builder.HasIndex(sr => sr.StructureNodePath)
                .HasDatabaseName("IX_StaffRoles_StructureNodePath");
        }
    }
}
