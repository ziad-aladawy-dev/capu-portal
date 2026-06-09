using CapitalUniversity.Modules.Payments.Application.Treasury;

namespace CapitalUniversity.Sync.Host.Scheduling;

/// <summary>
/// Hangfire entry point for the recurring Treasury receipt pull. Resolved from
/// DI per execution; delegates to <see cref="IReceiptSyncService"/>.
/// </summary>
public sealed class TreasuryReceiptPullTrigger
{
    private readonly IReceiptSyncService _sync;

    public TreasuryReceiptPullTrigger(IReceiptSyncService sync) => _sync = sync;

    public Task RunAsync(CancellationToken cancellationToken) => _sync.SyncAsync(cancellationToken);
}
