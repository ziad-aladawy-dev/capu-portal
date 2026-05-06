using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Academic;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CourseCode).IsRequired().HasMaxLength(20);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CreditHours).IsRequired();

        builder.HasOne(c => c.Level)
            .WithMany(l => l.Courses)
            .HasForeignKey(c => c.LevelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-reference for prerequisite
        builder.HasOne(c => c.PrerequisiteCourse)
            .WithMany()
            .HasForeignKey(c => c.PrerequisiteCourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}