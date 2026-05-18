using CapitalUniversity.Core.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProviderTransactionId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.RawPayloadJson).IsRequired();
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        // De-duplication is per-invoice — the same key on two different invoices
        // is a coincidence, not a replay. Unique index enforces idempotency at
        // the schema level so a retried webhook can never double-record.
        builder.HasIndex(x => new { x.InvoiceId, x.IdempotencyKey }).IsUnique();
    }
}
