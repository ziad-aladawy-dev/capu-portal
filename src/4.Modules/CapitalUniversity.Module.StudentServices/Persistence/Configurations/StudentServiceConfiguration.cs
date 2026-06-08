using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class StudentServiceConfiguration : IEntityTypeConfiguration<StudentService>
{
    public void Configure(EntityTypeBuilder<StudentService> builder)
    {
        builder.ToTable("StudentServices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.RequiresPayment).IsRequired();
        builder.Property(x => x.AllowedProcessingRoleIdsCsv).HasMaxLength(2048);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Soft-delete global query filter — same convention as Invoice +
        // StudentProfileRecord (per RemediationPlan P0.6 / P1.5).
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Workflow link is by id only; no EF navigation. The workflow lives
        // in the same module so a navigation would be physically possible,
        // but the value-object workflow can also be detached/swapped without
        // touching the service row.
        builder.Property(x => x.WorkflowDefinitionId);

        builder.HasMany(x => x.Fields)
            .WithOne(f => f.StudentService!)
            .HasForeignKey(f => f.StudentServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne(d => d.StudentService!)
            .HasForeignKey(d => d.StudentServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filtered unique index on Code, scoped to live rows so a soft-
        // deleted row does not block a fresh insert of the same code.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.IsActive);
    }
}
