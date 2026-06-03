using CapitalUniversity.Sync.Finance.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Sync.Finance.Persistence.Configurations;

internal sealed class InvoiceOutboxConfiguration : IEntityTypeConfiguration<InvoiceOutboxEntity>
{
    public void Configure(EntityTypeBuilder<InvoiceOutboxEntity> builder)
    {
        builder.ToTable("invoices_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ExternalInvoiceId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Operation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.PayloadSchemaVersion).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_invoices_outbox_Status_CreatedAt");
    }
}
