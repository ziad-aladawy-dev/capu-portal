using CapitalUniversity.Core.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class CoursePrerequisiteConfiguration : IEntityTypeConfiguration<CoursePrerequisite>
{
    public void Configure(EntityTypeBuilder<CoursePrerequisite> builder)
    {
        builder.ToTable("CoursePrerequisites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId).IsRequired();
        builder.Property(x => x.PrerequisiteCourseId).IsRequired();

        // One edge per (course, prerequisite) pair — schema constraint so the
        // graph cannot accumulate duplicate edges even if API validation slips.
        builder.HasIndex(x => new { x.CourseId, x.PrerequisiteCourseId })
            .IsUnique();

        // Reverse-lookup path: "which courses require X?" (delete usage-guard
        // and catalog health queries).
        builder.HasIndex(x => x.PrerequisiteCourseId);

        // Schema-level FKs to the catalog. No EF navigation per the catalog
        // read-through rule, but Restrict so a course referenced by any
        // prerequisite edge cannot be hard-deleted underneath the graph.
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(x => x.PrerequisiteCourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
