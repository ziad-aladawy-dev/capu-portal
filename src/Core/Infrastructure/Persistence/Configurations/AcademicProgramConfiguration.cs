using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class AcademicProgramConfiguration : IEntityTypeConfiguration<AcademicProgram>
{
    public void Configure(EntityTypeBuilder<AcademicProgram> builder)
    {
        builder.ToTable("AcademicPrograms");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(30);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.TotalHours).IsRequired(false);

        // Self-referencing for parent-child (program -> subprogram)
        builder.HasOne(p => p.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.FacultySystem)
            .WithMany(fs => fs.AcademicPrograms)
            .HasForeignKey(p => p.FacultySystemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}