using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class WorkflowStepActionConfiguration : IEntityTypeConfiguration<WorkflowStepAction>
{
    public void Configure(EntityTypeBuilder<WorkflowStepAction> builder)
    {
        builder.ToTable("WorkflowStepActions", "StudentServices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.TriggersSubmission).IsRequired();

        builder.HasIndex(x => new { x.WorkflowStepId, x.ActionKey }).IsUnique();
    }
}