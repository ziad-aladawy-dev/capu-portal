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

        // Code is upper-cased in the entity setter; the case-insensitive
        // collation is belt-and-braces in case rows arrive via raw SQL.
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(32)
            .UseCollation("SQL_Latin1_General_CP1_CI_AS");

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

        // ExternallySourced — composed data block flattened onto the table
        // via OwnsOne. See StudentConfiguration for rationale.
        builder.OwnsOne(x => x.ExternallySourced, ec =>
        {
            ec.Property(p => p.ExternalId).HasColumnName("ExternalId").HasMaxLength(128);
            ec.Property(p => p.ExternalUpdatedAt).HasColumnName("ExternalUpdatedAt");
            ec.Property(p => p.ExternalVersion).HasColumnName("ExternalVersion");
            ec.Property(p => p.LastSyncedAt).HasColumnName("LastSyncedAt");
            ec.Property(p => p.OriginSystem).HasColumnName("OriginSystem").HasMaxLength(32).IsRequired();
            ec.HasIndex(p => p.ExternalId)
                .IsUnique()
                .HasFilter("[ExternalId] IS NOT NULL");
        });
    }
}
