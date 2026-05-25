using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
{
    public void Configure(EntityTypeBuilder<WorkflowState> builder)
    {
        builder.ToTable("StudentServiceWorkflowStates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkflowDefinitionId).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.IsInitial).IsRequired();
        builder.Property(x => x.IsTerminal).IsRequired();
        builder.Property(x => x.IsWaitingPayment).IsRequired();

        // One row per (workflow, status) — duplicates would break transition
        // lookup.
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Status }).IsUnique();
    }
}
