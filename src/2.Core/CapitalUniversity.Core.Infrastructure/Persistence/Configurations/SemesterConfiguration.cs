using CapitalUniversity.Core.Domain.Semsters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Order)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired();

        // H7 — filtered UNIQUE index keyed on (AcademicYearId, IsCurrent) so
        // at most one semester per academic year can be marked current. See
        // the matching note on AcademicYearConfiguration. The composite key
        // matches the resolver's per-year scope.
        builder.HasIndex(x => new { x.AcademicYearId, x.IsCurrent })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");

        builder.HasOne(x => x.AcademicYear)
            .WithMany(x => x.Semesters)
            .HasForeignKey(x => x.AcademicYearId);
    }
}
