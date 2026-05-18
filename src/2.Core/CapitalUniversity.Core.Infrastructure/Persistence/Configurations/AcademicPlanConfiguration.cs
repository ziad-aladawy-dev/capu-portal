using CapitalUniversity.Core.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class AcademicPlanConfiguration : IEntityTypeConfiguration<AcademicPlan>
{
    public void Configure(EntityTypeBuilder<AcademicPlan> builder)
    {
        builder.ToTable("AcademicPlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.StructureNodeId).IsRequired();
        builder.Property(x => x.EffectiveFrom).IsRequired();

        // Plan picker hot path: list active plans for a structure node.
        builder.HasIndex(x => new { x.StructureNodeId, x.IsActive });

        builder.HasMany(x => x.PlanCourses)
            .WithOne(x => x.AcademicPlan)
            .HasForeignKey(x => x.AcademicPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
