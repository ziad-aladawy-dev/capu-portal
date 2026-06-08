using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Modules.Registration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.Registration.Persistence.Configurations;

public class StudentRegisteredCourseConfiguration : IEntityTypeConfiguration<StudentRegisteredCourse>
{
    public void Configure(EntityTypeBuilder<StudentRegisteredCourse> builder)
    {
        builder.ToTable("StudentRegisteredCourses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.CourseId).IsRequired();
        builder.Property(x => x.SemesterId).IsRequired();
        builder.Property(x => x.StructureNodeId).IsRequired();

        builder.Property(x => x.AttemptNumber).IsRequired();
        builder.Property(x => x.RegistrationStatus).HasConversion<int>().IsRequired();

        builder.Property(x => x.RegisteredAt).IsRequired();
        builder.Property(x => x.CompletedAt);

        // Schema-level FKs to cross-module roots. No navigation per the
        // modularity rule; Restrict so a student/course/term/node referenced by
        // a registration cannot be silently removed underneath it.
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Semester>()
            .WithMany()
            .HasForeignKey(x => x.SemesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StructureNode>()
            .WithMany()
            .HasForeignKey(x => x.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One attempt ordinal per (student, course) — preserves repeat ordering
        // and makes a replayed sync tick idempotent on the natural key. The
        // term is intentionally NOT part of this key: a repeat lands in a later
        // term but must still get the next attempt number, not collide.
        builder.HasIndex(x => new { x.StudentId, x.CourseId, x.AttemptNumber })
            .IsUnique();

        // Hot path — "my current registrations": filter by student + status.
        builder.HasIndex(x => new { x.StudentId, x.RegistrationStatus });

        // History path — "my registrations grouped by term".
        builder.HasIndex(x => new { x.StudentId, x.SemesterId });

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
