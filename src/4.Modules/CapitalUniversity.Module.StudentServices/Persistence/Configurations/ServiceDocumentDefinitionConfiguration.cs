using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class ServiceDocumentDefinitionConfiguration : IEntityTypeConfiguration<ServiceDocumentDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceDocumentDefinition> builder)
    {
        builder.ToTable("ServiceDocumentDefinitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentServiceId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(512);
        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.AllowedExtensions).HasMaxLength(512);
        builder.Property(x => x.MaxFileSizeBytes).IsRequired();

        builder.HasIndex(x => new { x.StudentServiceId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.StudentServiceId, x.DisplayOrder });
    }
}
