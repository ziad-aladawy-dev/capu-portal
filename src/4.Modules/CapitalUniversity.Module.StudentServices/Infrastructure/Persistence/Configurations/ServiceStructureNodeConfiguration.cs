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

        builder.HasOne(x => x.Service)
            .WithMany(x => x.ScopeNodes)
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.StructureNode>()
            .WithMany()
            .HasForeignKey(x => x.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ServiceStructureNodes_StructureNodes_StructureNodeId");
    }
}