using CapitalUniversity.Modules.AcademicRecords.Domain;
using CapitalUniversity.Modules.Registration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.AcademicRecords.Persistence.Configurations;

public class StudentAcademicResultConfiguration : IEntityTypeConfiguration<StudentAcademicResult>
{
    public void Configure(EntityTypeBuilder<StudentAcademicResult> builder)
    {
        builder.ToTable("StudentAcademicResults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentRegisteredCourseId).IsRequired();

        builder.Property(x => x.Grade).HasMaxLength(8);
        builder.Property(x => x.NumericScore).HasPrecision(6, 3);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreditsEarned).IsRequired();
        builder.Property(x => x.IsLatestAttempt).IsRequired();

        // Schema-level FK to the registration attempt this result grades. No
        // navigation per the modularity rule; Restrict so a registration that a
        // result points at cannot be silently removed underneath it.
        builder.HasOne<StudentRegisteredCourse>()
            .WithMany()
            .HasForeignKey(x => x.StudentRegisteredCourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // One result row per registration attempt — makes a replayed sync tick
        // idempotent on the natural key.
        builder.HasIndex(x => x.StudentRegisteredCourseId).IsUnique();

        // Transcript path — "latest-attempt results": the transcript query filters
        // on this flag after joining to the student's registrations.
        builder.HasIndex(x => x.IsLatestAttempt);

        // ExternallySourced — composed data block flattened onto the table.
        // ExternalId is the sync merge key (filtered-unique so multiple
        // never-synced rows can coexist while synced rows stay unique).
        builder.OwnsOne(x => x.ExternallySourced, ec =>
        {
            ec.Property(p => p.ExternalId).HasColumnName("ExternalId").HasMaxLength(128);
            ec.Property(p => p.ExternalUpdatedAt).HasColumnName("ExternalUpdatedAt");
            ec.Property(p => p.ExternalVersion).HasColumnName("ExternalVersion");
            ec.Property(p => p.LastSyncedAt).HasColumnName("LastSyncedAt");
            ec.Property(p => p.OriginSystem).HasColumnName("OriginSystem").HasMaxLength(32).IsRequired();
            ec.HasIndex(p => p.ExternalId)
                .IsUnique()
                .HasFilter("[ExternalId] IS NOT NULL");
        });
    }
}
