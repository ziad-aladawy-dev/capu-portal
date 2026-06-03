using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class RequestHistoryEntryConfiguration : IEntityTypeConfiguration<RequestHistoryEntry>
{
    public void Configure(EntityTypeBuilder<RequestHistoryEntry> builder)
    {
        builder.ToTable("RequestHistoryEntries", "StudentServices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.PerformedByRole).HasMaxLength(50);
        builder.Property(x => x.PerformedAt).IsRequired();
        builder.HasIndex(x => x.StudentRequestId);
    }
}