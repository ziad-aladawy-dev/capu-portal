using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StaffRoleConfiguration : IEntityTypeConfiguration<StaffRole>
{
    public void Configure(EntityTypeBuilder<StaffRole> builder)
    {
        builder.ToTable("StaffRoles");
        builder.HasKey(sr => sr.Id);

        builder.HasOne(sr => sr.Staff)
            .WithMany(s => s.Roles)
            .HasForeignKey(sr => sr.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sr => sr.Role)
            .WithMany(r => r.StaffRoles)
            .HasForeignKey(sr => sr.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sr => sr.Faculty)
            .WithMany()
            .HasForeignKey(sr => sr.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.AcademicProgram)
            .WithMany()
            .HasForeignKey(sr => sr.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}