using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.AcademicCalendar;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");
        builder.HasKey(ay => ay.Id);
        builder.Property(ay => ay.Name).IsRequired().HasMaxLength(50);
        builder.HasIndex(ay => ay.IsCurrent);
    }
}