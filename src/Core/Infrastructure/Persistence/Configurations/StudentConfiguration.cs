using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.NationalId).IsRequired().HasMaxLength(20);
        builder.Property(s => s.StudentCode).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Email).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Phone).HasMaxLength(20);

        builder.HasIndex(s => s.NationalId).IsUnique();
        builder.HasIndex(s => s.StudentCode).IsUnique();
        builder.HasIndex(s => s.Email).IsUnique();

        builder.HasOne(s => s.Faculty)
            .WithMany()
            .HasForeignKey(s => s.FacultyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AcademicProgram)
            .WithMany()
            .HasForeignKey(s => s.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Level)
            .WithMany()
            .HasForeignKey(s => s.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CurrentAcademicYear)
            .WithMany()
            .HasForeignKey(s => s.CurrentAcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CurrentSemester)
            .WithMany()
            .HasForeignKey(s => s.CurrentSemesterId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}