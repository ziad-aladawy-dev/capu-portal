using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Alias to the Identity.Student entity to keep the cross-module FK declarative.
using StudentEntity = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class StudentServiceRequestConfiguration : IEntityTypeConfiguration<StudentServiceRequest>
{
    public void Configure(EntityTypeBuilder<StudentServiceRequest> builder)
    {
        builder.ToTable("StudentServiceRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.StudentServiceId).IsRequired();
        builder.Property(x => x.CurrentStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.SubmittedAt);
        builder.Property(x => x.ProcessedAt);
        builder.Property(x => x.AssignedStaffId);
        builder.Property(x => x.CancellationReason).HasMaxLength(1024);
        builder.Property(x => x.RejectionReason).HasMaxLength(1024);
        builder.Property(x => x.PaymentReferenceId);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasQueryFilter(x => !x.IsDeleted);

        // Schema-level FK to the Student aggregate — no navigation per the
        // modularity rule, but the FK enforces referential integrity. Restrict
        // delete so a student row can't be removed while requests still
        // reference it (consistent with StudentProfileRecord).
        builder.HasOne<StudentEntity>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StudentService)
            .WithMany()
            .HasForeignKey(x => x.StudentServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.FieldValues)
            .WithOne(v => v.StudentServiceRequest!)
            .HasForeignKey(v => v.StudentServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne(d => d.StudentServiceRequest!)
            .HasForeignKey(d => d.StudentServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hot list queries: "this student's requests" + "this service's
        // requests" + "pending queue ordered by submission time".
        builder.HasIndex(x => new { x.StudentId, x.CurrentStatus });
        builder.HasIndex(x => new { x.StudentServiceId, x.CurrentStatus });
        builder.HasIndex(x => new { x.CurrentStatus, x.SubmittedAt });
        builder.HasIndex(x => x.AssignedStaffId);
        builder.HasIndex(x => x.PaymentReferenceId);
    }
}
