using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Users;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.NationalId).IsRequired().HasMaxLength(20);
        builder.Property(s => s.StudentCode).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Email).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Phone).HasMaxLength(20);

        builder.HasIndex(s => s.NationalId).IsUnique();
        builder.HasIndex(s => s.StudentCode).IsUnique();
        builder.HasIndex(s => s.Email).IsUnique();
    }
}