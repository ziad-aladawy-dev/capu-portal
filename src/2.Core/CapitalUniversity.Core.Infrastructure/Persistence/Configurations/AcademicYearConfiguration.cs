using CapitalUniversity.Core.Domain.Semsters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired();

        // H7 — filtered UNIQUE index. The previous non-unique index served only
        // as a lookup; in concert with the AcademicTimelineBackgroundService
        // resolver, two concurrent writers could each set IsCurrent=1 on a
        // different row and the database would accept both. Making the index
        // unique pushes the invariant into SQL Server: at most one row may
        // carry IsCurrent=1 at any moment. The resolver in
        // AcademicYearService deactivates-before-activates in two flushes so
        // a single SaveChanges never momentarily breaks the constraint.
        builder.HasIndex(x => x.IsCurrent)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.HasMany(x => x.Semesters)
            .WithOne(x => x.AcademicYear)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
