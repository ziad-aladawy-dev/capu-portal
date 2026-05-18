using CapitalUniversity.Core.Domain.StudentInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

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

        // Hottest lookup: "show me this student's records" + filter by category.
        builder.HasIndex(x => new { x.StudentId, x.Category });

        // Filtered partial index for sensitive records — audit queries hit a
        // narrow set, not the full table.
        builder.HasIndex(x => x.IsSensitive).HasFilter("[IsSensitive] = 1");
    }
}
