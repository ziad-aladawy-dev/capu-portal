using CapitalUniversity.Core.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreditHours)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasConversion<int>()
            .IsRequired();

        // Unique catalog code, case-insensitive at SQL default collation. The
        // catalog is school-wide so the constraint is global, not scope-bound.
        builder.HasIndex(x => x.Code).IsUnique();

        // Browse-and-filter common path: list active courses.
        builder.HasIndex(x => x.IsActive);
    }
}
