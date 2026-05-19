using CapitalUniversity.Core.Domain.Authorization;
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
        builder.Property(s => s.DisplayName).IsRequired().HasMaxLength(200);

        builder.HasOne(s => s.Module)
            .WithMany(m => m.Services)
            .HasForeignKey(s => s.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // OrderNumber is a UI sort hint, not an identity. Multiple services
        // within a module can legitimately share an OrderNumber — e.g. the
        // legacy DataSeeder rows ("View Permissions" at 0) coexisting with
        // manifest-declared rows ("Create Permissions" at 1) that the
        // PermissionManifestSynchronizer adds keyed on (ModuleId, DisplayName).
        // The previous unique index collided that flow on every fresh-DB boot.
        builder.HasIndex(s => new { s.ModuleId, s.OrderNumber });
    }
}