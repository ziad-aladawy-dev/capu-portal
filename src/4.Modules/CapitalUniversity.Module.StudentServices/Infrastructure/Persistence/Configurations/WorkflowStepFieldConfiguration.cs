using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class WorkflowStepFieldConfiguration : IEntityTypeConfiguration<WorkflowStepField>
{
    public void Configure(EntityTypeBuilder<WorkflowStepField> builder)
    {
        builder.ToTable("WorkflowStepFields", "StudentServices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FieldType).HasConversion<int>().IsRequired();
        builder.Property(x => x.OptionsJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.WorkflowStepId, x.Order });
    }
}