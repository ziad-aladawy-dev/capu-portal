using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "StudentServices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsPaid).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);

        builder.OwnsOne(x => x.Scope, scope =>
        {
            scope.Property(p => p.IsGlobalStructural).HasColumnName("ScopeIsGlobalStructural");
            scope.Property(p => p.StructureNodeId).HasColumnName("ScopeStructureNodeId");
            scope.Property(p => p.IncludeDescendants).HasColumnName("ScopeIncludeDescendants");
            scope.Property(p => p.StructureNodePath).HasColumnName("ScopeStructureNodePath").HasMaxLength(4000);
            scope.Property(p => p.IsGlobalTemporal).HasColumnName("ScopeIsGlobalTemporal");
            scope.Property(p => p.Year).HasColumnName("ScopeYear").HasMaxLength(50);
            scope.Property(p => p.Semester).HasColumnName("ScopeSemester").HasMaxLength(50);
        });

        builder.HasOne(x => x.Workflow)
            .WithMany()
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.FormFieldsJson)
             .IsRequired()
             .HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.WorkflowId);
    }
}