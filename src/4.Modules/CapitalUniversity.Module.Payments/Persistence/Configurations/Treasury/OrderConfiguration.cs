using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Gateway).HasConversion<int>().IsRequired();
        builder.Property(x => x.MerchantOrderId).HasMaxLength(128);
        builder.Property(x => x.RedirectUrl).HasMaxLength(2048);
        builder.Property(x => x.GatewaySessionRef).HasMaxLength(512);
        builder.Property(x => x.BaseAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CollectionFees).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.ItemCount).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasQueryFilter(x => !x.IsDeleted);

        // MerchantOrderId is the primary external identifier — unique once set.
        // Empty-string rows (orders not yet initiated) are excluded by the filter.
        builder.HasIndex(x => x.MerchantOrderId)
            .IsUnique()
            .HasFilter("[MerchantOrderId] <> ''");

        // Reconciliation sweep: stale PendingPayment orders.
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // A fee is released back to Pending (OrderId -> null) when its order
        // fails/expires, so SetNull, not Cascade.
        builder.HasMany(x => x.Fees)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Transactions)
            .WithOne(t => t.Order)
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
