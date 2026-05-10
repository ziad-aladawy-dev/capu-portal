using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Users;

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

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.Faculty>()
            .WithMany()
            .HasForeignKey(rps => rps.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.AcademicProgram>()
            .WithMany()
            .HasForeignKey(rps => rps.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}