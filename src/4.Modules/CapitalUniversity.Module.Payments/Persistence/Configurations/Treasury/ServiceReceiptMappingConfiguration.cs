using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class ServiceReceiptMappingConfiguration : IEntityTypeConfiguration<ServiceReceiptMapping>
{
    public void Configure(EntityTypeBuilder<ServiceReceiptMapping> builder)
    {
        builder.ToTable("ServiceReceiptMappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentServiceId).IsRequired();
        builder.Property(x => x.ReceiptId).IsRequired();
        builder.Property(x => x.QuantityMode).HasConversion<int>().IsRequired();

        builder.HasQueryFilter(x => !x.IsDeleted);

        // At most one ACTIVE mapping per service.
        builder.HasIndex(x => x.StudentServiceId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasOne(x => x.Receipt)
            .WithMany()
            .HasForeignKey(x => x.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
