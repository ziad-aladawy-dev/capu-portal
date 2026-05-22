using CapitalUniversity.Core.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MessageType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        // Dispatcher query: WHERE ProcessedAt IS NULL ORDER BY EnqueuedAt — index
        // covers both predicate + sort.
        builder.HasIndex(x => new { x.ProcessedAt, x.EnqueuedAt });

        // Operator query for the poison queue ("show me everything that's stuck").
        builder.HasIndex(x => new { x.IsPoisoned, x.PoisonedAt });
    }
}
