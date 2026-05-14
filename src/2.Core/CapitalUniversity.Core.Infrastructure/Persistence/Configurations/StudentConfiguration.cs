using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(
        EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.StudentCode)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.NationalId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.NationalId)
            .IsUnique();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.HasOne(x => x.StructureNode)
            .WithMany()
            .HasForeignKey(x => x.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}