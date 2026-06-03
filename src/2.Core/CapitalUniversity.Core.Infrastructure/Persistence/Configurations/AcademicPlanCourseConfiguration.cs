using CapitalUniversity.Core.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class AcademicPlanCourseConfiguration : IEntityTypeConfiguration<AcademicPlanCourse>
{
    public void Configure(EntityTypeBuilder<AcademicPlanCourse> builder)
    {
        builder.ToTable("AcademicPlanCourses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId).IsRequired();
        builder.Property(x => x.Level).IsRequired();
        builder.Property(x => x.Semester).IsRequired();

        // A catalog course appears at most once in a plan. Schema constraint so
        // the composition cannot drift even if API validation slips.
        builder.HasIndex(x => new { x.AcademicPlanId, x.CourseId })
            .IsUnique();

        // Per-level/semester layout query.
        builder.HasIndex(x => new { x.AcademicPlanId, x.Level, x.Semester });

        // M1 fix — schema-level FK to the catalog Course. No EF navigation per
        // the catalog read-through rule, but Restrict so a course referenced by
        // any plan cannot be hard-deleted underneath the composition (the
        // service adds a matching usage-guard for a clean Conflict response).
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
