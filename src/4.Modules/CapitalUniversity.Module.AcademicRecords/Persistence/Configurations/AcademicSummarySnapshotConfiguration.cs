using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.AcademicRecords.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.AcademicRecords.Persistence.Configurations;

public class AcademicSummarySnapshotConfiguration : IEntityTypeConfiguration<AcademicSummarySnapshot>
{
    public void Configure(EntityTypeBuilder<AcademicSummarySnapshot> builder)
    {
        builder.ToTable("AcademicSummarySnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();

        builder.Property(x => x.Gpa).HasPrecision(5, 2);
        builder.Property(x => x.Cgpa).HasPrecision(5, 2);
        builder.Property(x => x.EarnedCredits).IsRequired();
        builder.Property(x => x.RemainingCredits).IsRequired();
        builder.Property(x => x.PassedHours).IsRequired();
        builder.Property(x => x.FailedHours).IsRequired();
        builder.Property(x => x.AcademicStanding).HasMaxLength(128);

        // Schema-level FK to the student root. No navigation per the modularity
        // rule; Restrict so a student carrying a summary cannot be silently removed.
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // One current summary per student — the sync layer upserts this row.
        builder.HasIndex(x => x.StudentId).IsUnique();

        // ExternallySourced — composed data block flattened onto the table.
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
