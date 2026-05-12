using CapitalUniversity.Core.Domain.UniversityStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StructureNodeConfiguration : IEntityTypeConfiguration<StructureNode>
{
    public void Configure(EntityTypeBuilder<StructureNode> builder)
    {
        builder.ToTable("StructureNodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.Order)
            .HasDefaultValue(0);

        builder.Property(x => x.Path)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.Depth)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentId);

        builder.HasIndex(x => x.Path);

        builder.HasIndex(x => x.Depth);

        builder.HasIndex(x => x.Type);

        builder.HasIndex(x => x.Order);

        builder.HasIndex(x => x.IsDeleted);
    }
}