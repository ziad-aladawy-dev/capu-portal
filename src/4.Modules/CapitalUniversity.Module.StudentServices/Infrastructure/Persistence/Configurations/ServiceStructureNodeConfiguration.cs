using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class ServiceStructureNodeConfiguration : IEntityTypeConfiguration<ServiceStructureNode>
{
    public void Configure(EntityTypeBuilder<ServiceStructureNode> builder)
    {
        builder.ToTable("ServiceStructureNodes", "StudentServices");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ServiceId, x.StructureNodeId }).IsUnique();

        builder.HasOne(x => x.StructureNode)
            .WithMany()
            .HasForeignKey(x => x.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}