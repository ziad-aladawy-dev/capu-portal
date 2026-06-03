using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps", "StudentServices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.StepType).HasConversion<int>().IsRequired();

        builder.HasIndex(x => new { x.WorkflowId, x.Order });

        builder.HasMany(x => x.Fields)
            .WithOne(x => x.WorkflowStep)
            .HasForeignKey(x => x.WorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}