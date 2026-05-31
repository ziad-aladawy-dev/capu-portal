using CapitalUniversity.Sync.Student.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Student.Persistence.Configurations;

internal sealed class StudentEntityConfiguration : IEntityTypeConfiguration<StudentEntity>
{
    public void Configure(EntityTypeBuilder<StudentEntity> builder)
    {
        builder.ToTable("students");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalStudentId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OriginSystem).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.ExternalStudentId).IsUnique();
        builder.HasIndex(x => x.ExternalUpdatedAt);
    }
}