using CapitalUniversity.Sync.Courses.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Courses.Persistence.Configurations;

internal sealed class CourseOutboxConfiguration : IEntityTypeConfiguration<CourseOutboxEntity>
{
    public void Configure(EntityTypeBuilder<CourseOutboxEntity> builder)
    {
        builder.ToTable("courses_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalCourseId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.PayloadSchemaVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_courses_outbox_Status_CreatedAt");
    }
}
