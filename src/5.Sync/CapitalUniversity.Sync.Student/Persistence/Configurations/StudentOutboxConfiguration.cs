using CapitalUniversity.Sync.Student.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Student.Persistence.Configurations;

internal sealed class StudentOutboxConfiguration : IEntityTypeConfiguration<StudentOutboxEntity>
{
    public void Configure(EntityTypeBuilder<StudentOutboxEntity> builder)
    {
        builder.ToTable("student_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalStudentId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.PayloadSchemaVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        // Push extractor reads Pending rows in CreatedAt order; this composite index
        // keeps that read on a covering scan as long as the Pending population stays
        // small relative to total rows (the steady state).
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_student_outbox_Status_CreatedAt");
    }
}