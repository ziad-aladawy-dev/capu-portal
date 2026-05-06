using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("Modules");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ModuleKey).IsRequired().HasMaxLength(50);
        builder.Property(m => m.DisplayNameAr).IsRequired().HasMaxLength(200);
        builder.Property(m => m.DisplayNameEn).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Icon).HasMaxLength(100);

        builder.HasIndex(m => m.ModuleKey).IsUnique();
    }
}