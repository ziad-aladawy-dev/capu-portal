
using CapitalUniversity.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.FeeType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(500);

        // Reconciliation query — "every fee this module wrote against this reference".
        builder.HasIndex(x => new { x.SourceModule, x.ReferenceId });
    }
}
