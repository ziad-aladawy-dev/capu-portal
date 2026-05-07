using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

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

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.Faculty>()
            .WithMany()
            .HasForeignKey(sr => sr.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.AcademicProgram>()
            .WithMany()
            .HasForeignKey(sr => sr.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}