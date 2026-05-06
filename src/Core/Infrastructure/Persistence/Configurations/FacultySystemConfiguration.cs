using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class FacultySystemConfiguration : IEntityTypeConfiguration<FacultySystem>
{
    public void Configure(EntityTypeBuilder<FacultySystem> builder)
    {
        builder.ToTable("FacultySystems");
        builder.HasKey(fs => fs.Id);

        builder.HasOne(fs => fs.Faculty)
            .WithMany(f => f.Systems)
            .HasForeignKey(fs => fs.FacultyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}