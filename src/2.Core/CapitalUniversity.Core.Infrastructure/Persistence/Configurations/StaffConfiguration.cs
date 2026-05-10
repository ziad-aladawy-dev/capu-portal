using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.Users;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staffs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.NationalId).IsRequired().HasMaxLength(20);
        builder.Property(s => s.StaffCode).IsRequired().HasMaxLength(20);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Email).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Phone).HasMaxLength(20);

        builder.HasIndex(s => s.NationalId).IsUnique();
        builder.HasIndex(s => s.StaffCode).IsUnique();
        builder.HasIndex(s => s.Email).IsUnique();

        builder.HasOne<CapitalUniversity.Core.Domain.UniversityStructure.University>()
            .WithMany()
            .HasForeignKey(s => s.UniversityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}