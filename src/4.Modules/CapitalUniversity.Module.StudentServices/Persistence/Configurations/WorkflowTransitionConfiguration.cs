using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("StudentServiceWorkflowTransitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowDefinitionId).IsRequired();
        builder.Property(x => x.FromStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.ToStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.TransitionType).HasConversion<int>().IsRequired();
        builder.Property(x => x.RequiredAction).HasMaxLength(64);

        // One transition per (workflow, from, to) — duplicate transitions
        // would make resolution ambiguous.
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStatus, x.ToStatus }).IsUnique();
    }
}
