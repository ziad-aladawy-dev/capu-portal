using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Gateway).HasConversion<int>().IsRequired();
        builder.Property(x => x.MerchantOrderId).HasMaxLength(128);

        // One payment per fee — schema-level idempotency.
        builder.HasIndex(x => x.FeeId).IsUnique();
        builder.HasIndex(x => x.OrderId);

        builder.HasOne<StudentFee>()
            .WithMany()
            .HasForeignKey(x => x.FeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Append-only: no soft-delete filter, no update path.
    }
}
