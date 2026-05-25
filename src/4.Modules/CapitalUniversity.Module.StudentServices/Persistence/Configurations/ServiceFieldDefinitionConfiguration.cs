using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class ServiceFieldDefinitionConfiguration : IEntityTypeConfiguration<ServiceFieldDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceFieldDefinition> builder)
    {
        builder.ToTable("ServiceFieldDefinitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentServiceId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(512);
        builder.Property(x => x.FieldType).HasConversion<int>().IsRequired();
        builder.Property(x => x.IsRequired).IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.MinValue).HasColumnType("decimal(18,4)");
        builder.Property(x => x.MaxValue).HasColumnType("decimal(18,4)");
        builder.Property(x => x.DropdownValues).HasMaxLength(4000);

        // (StudentServiceId, Name) is unique per service — duplicate field
        // machine names would break form rendering.
        builder.HasIndex(x => new { x.StudentServiceId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.StudentServiceId, x.DisplayOrder });
    }
}
