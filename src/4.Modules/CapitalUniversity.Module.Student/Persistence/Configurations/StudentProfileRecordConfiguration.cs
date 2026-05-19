using CapitalUniversity.Modules.Student.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Alias the Student entity to avoid the name clash with the enclosing
// `CapitalUniversity.Modules.Student` namespace — `using` of
// CapitalUniversity.Core.Domain.Identity makes the unqualified `Student`
// identifier ambiguous with the parent namespace.
using StudentEntity = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Modules.Student.Persistence.Configurations;

public class StudentProfileRecordConfiguration : IEntityTypeConfiguration<StudentProfileRecord>
{
    public void Configure(EntityTypeBuilder<StudentProfileRecord> builder)
    {
        builder.ToTable("StudentProfileRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.Category).HasConversion<int>().IsRequired();
        builder.Property(x => x.CustomCategoryKey).HasMaxLength(64);
        builder.Property(x => x.SchemaVersion).IsRequired();
        builder.Property(x => x.DataJson).HasColumnType("nvarchar(max)").IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        // P0.6 / P1.5 — soft-delete global query filter. Declared here because
        // the StudentProfileRecord type lives in Module.Student; CoreDbContext
        // (Core.Infrastructure) cannot reference this type at the
        // modelBuilder.Entity<T>() call site.
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Schema-level FK to Student. No navigation per the modularity rule.
        builder.HasOne<StudentEntity>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Hottest lookup: "show me this student's records" + filter by category.
        builder.HasIndex(x => new { x.StudentId, x.Category });

        // Filtered partial index for sensitive records — audit queries hit a
        // narrow set, not the full table.
        builder.HasIndex(x => x.IsSensitive).HasFilter("[IsSensitive] = 1");

        // P3.4: one live record per (Student, Category, CustomCategoryKey).
        // Filtered on IsDeleted = 0 so a soft-deleted row can be re-added.
        builder.HasIndex(x => new { x.StudentId, x.Category, x.CustomCategoryKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
