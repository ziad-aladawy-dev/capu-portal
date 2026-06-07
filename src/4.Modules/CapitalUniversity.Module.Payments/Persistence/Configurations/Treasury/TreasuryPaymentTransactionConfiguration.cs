using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuration for the new Treasury audit transaction. Maps to
/// <c>TreasuryPaymentTransactions</c> — distinct from the legacy
/// <c>PaymentTransactions</c> table, which keeps its existing entity/config.
/// </summary>
public class TreasuryPaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("TreasuryPaymentTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MerchantOrderId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Gateway).HasConversion<int>().IsRequired();
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.GatewayReference).HasMaxLength(256);
        builder.Property(x => x.RawResponse).IsRequired();
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        // Webhook/event dedup at the audit layer.
        builder.HasIndex(x => new { x.MerchantOrderId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.OrderId);

        // Order FK configured from the Order side (HasMany Transactions, Cascade).
        // Append-only: no soft-delete filter.
    }
}
