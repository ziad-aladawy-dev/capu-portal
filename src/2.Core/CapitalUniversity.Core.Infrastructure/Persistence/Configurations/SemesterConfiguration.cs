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

        builder.HasIndex(x => x.IsCurrent)
            .HasFilter("[IsCurrent] = 1");

        builder.HasOne(x => x.AcademicYear)
            .WithMany(x => x.Semesters)
            .HasForeignKey(x => x.AcademicYearId);
    }
}
