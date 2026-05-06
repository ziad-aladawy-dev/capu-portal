using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.ToTable("Faculties");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(f => f.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Code).IsRequired().HasMaxLength(20);

        builder.HasOne(f => f.University)
            .WithMany(u => u.Faculties)
            .HasForeignKey(f => f.UniversityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}