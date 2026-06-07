using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class TreasuryReceiptConfiguration : IEntityTypeConfiguration<TreasuryReceipt>
{
    public void Configure(EntityTypeBuilder<TreasuryReceipt> builder)
    {
        builder.ToTable("TreasuryReceipts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalReceiptId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ConnectionTypeId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.UnitAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasQueryFilter(x => !x.IsDeleted);

        // Hottest read: "active receipts for the relevant connection type".
        builder.HasIndex(x => new { x.ConnectionTypeId, x.IsActive });

        // ExternallySourced — flattened onto the row via OwnsOne, filtered-unique
        // ExternalId. Mirrors InvoiceConfiguration's provenance block.
        builder.OwnsOne(x => x.ExternallySourced, ec =>
        {
            ec.Property(p => p.ExternalId).HasColumnName("ExternalId").HasMaxLength(128);
            ec.Property(p => p.ExternalUpdatedAt).HasColumnName("ExternalUpdatedAt");
            ec.Property(p => p.ExternalVersion).HasColumnName("ExternalVersion");
            ec.Property(p => p.LastSyncedAt).HasColumnName("LastSyncedAt");
            ec.Property(p => p.OriginSystem).HasColumnName("OriginSystem").HasMaxLength(32).IsRequired();
            ec.HasIndex(p => p.ExternalId)
                .IsUnique()
                .HasFilter("[ExternalId] IS NOT NULL");
        });
    }
}
