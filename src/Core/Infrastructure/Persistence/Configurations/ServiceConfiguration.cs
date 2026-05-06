using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DisplayNameAr).IsRequired().HasMaxLength(200);
        builder.Property(s => s.DisplayNameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(s => s.Module)
            .WithMany(m => m.Services)
            .HasForeignKey(s => s.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ModuleId, s.OrderNumber }).IsUnique();
    }
}