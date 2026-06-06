using CapitalUniversity.Sync.Finance.Push;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Finance.Persistence;

/// <summary>
/// Sync-side DbContext — outbox only. Operational invoice rows live in Core's
/// Payments module and are written through <c>ICoreWriteGateway</c>.
/// </summary>
public sealed class FinanceSyncDbContext : DbContext
{
    public const string SchemaName = "sync_finance";

    public FinanceSyncDbContext(DbContextOptions<FinanceSyncDbContext> options)
        : base(options) { }

    public DbSet<InvoiceOutboxEntity> InvoicesOutbox => Set<InvoiceOutboxEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceSyncDbContext).Assembly);
    }
}
