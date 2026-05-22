using CapitalUniversity.Core.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(100);
        builder.Property(r => r.DisplayName).IsRequired().HasMaxLength(200);

        builder.HasOne(r => r.Module)
            .WithMany(m => m.Resources)
            .HasForeignKey(r => r.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Key is the manifest-declared slug (e.g. "invoices", "academic-years")
        // and is the natural identifier inside a module. Synchroniser matches
        // existing rows by (ModuleId, Key) — duplicates would break that, so
        // the index is unique.
        builder.HasIndex(r => new { r.ModuleId, r.Key }).IsUnique();
        builder.HasIndex(r => new { r.ModuleId, r.OrderNumber });
    }
}
