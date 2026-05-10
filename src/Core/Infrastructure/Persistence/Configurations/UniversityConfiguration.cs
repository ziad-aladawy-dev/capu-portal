using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class UniversityConfiguration : IEntityTypeConfiguration<University>
{
    public void Configure(EntityTypeBuilder<University> builder)
    {
        builder.ToTable("Universities");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Domain).HasMaxLength(100);
        builder.Property(u => u.LogoUrl).HasMaxLength(500);
        builder.HasIndex(u => u.Domain).IsUnique();
    }
}