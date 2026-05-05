using CapitalUniversity.Core.Domain.UniversityStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection.Emit;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("Levels");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Code).IsRequired().HasMaxLength(20);
        builder.Property(l => l.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(l => l.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Order).IsRequired();

        builder.HasOne(l => l.AcademicProgram)
            .WithMany(p => p.Levels)
            .HasForeignKey(l => l.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}