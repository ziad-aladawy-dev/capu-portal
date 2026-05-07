using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

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

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.Faculty>()
            .WithMany()
            .HasForeignKey(sps => sps.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.AcademicProgram>()
            .WithMany()
            .HasForeignKey(sps => sps.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}