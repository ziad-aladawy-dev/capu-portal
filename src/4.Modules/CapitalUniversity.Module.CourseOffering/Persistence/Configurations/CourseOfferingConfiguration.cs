using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Modules.CourseOffering.Persistence.Configurations;

public class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOfferingEntity>
{
    public void Configure(EntityTypeBuilder<CourseOfferingEntity> builder)
    {
        builder.ToTable("CourseOfferings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CourseId).IsRequired();
        builder.Property(x => x.SemesterId).IsRequired();
        builder.Property(x => x.StructureNodeId).IsRequired();

        builder.Property(x => x.SectionCode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Capacity).IsRequired();
        builder.Property(x => x.RegisteredCount).IsRequired();

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.RegistrationState).HasConversion<int>().IsRequired();

        builder.Property(x => x.ExternalSystemId).HasMaxLength(128);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Soft-delete filter — declared here because the entity lives in the
        // module project; CoreDbContext (Core.Infrastructure) cannot reference
        // the type at modelBuilder.Entity<T>() call sites.
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Schema-level FKs to cross-module roots. No navigation per the
        // modularity rule; Restrict so a course/term/node referenced by an
        // offering cannot be silently removed underneath it.
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

        // One section identifier per (course, semester, node). Filtered on
        // IsDeleted = 0 so a soft-deleted section can be recreated. Mirrors
        // the StudentProfileRecord per-category uniqueness pattern.
        builder.HasIndex(x => new { x.CourseId, x.SemesterId, x.StructureNodeId, x.SectionCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Hot list path: "show me all offerings for this program this term".
        builder.HasIndex(x => new { x.StructureNodeId, x.SemesterId, x.Status });

        // Course-centric lookup: "where else is this course running this term?"
        builder.HasIndex(x => new { x.CourseId, x.SemesterId });
    }
}
