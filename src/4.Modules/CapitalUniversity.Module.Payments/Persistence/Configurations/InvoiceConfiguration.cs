using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // P0.6 / P1.5 — soft-delete global query filter. Declared here because
        // the Invoice type lives in Module.Payments; CoreDbContext (Core.Infra)
        // cannot reference this type at the modelBuilder.Entity<T>() call site.
        builder.HasQueryFilter(x => !x.IsDeleted);

        // List-by-student is the hottest path (student portal "my invoices").
        builder.HasIndex(x => new { x.StudentId, x.Status });

        // Schema-level FK to Student. No navigation per the modularity rule —
        // we still want SQL to reject orphaned invoices.
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Transactions)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ExternallySourced — composed data block flattened onto the Invoices
        // table via OwnsOne. See StudentConfiguration for rationale.
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
