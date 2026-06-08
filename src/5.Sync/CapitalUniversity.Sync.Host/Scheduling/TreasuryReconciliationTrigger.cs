using CapitalUniversity.Modules.Payments.Abstractions.Treasury;

namespace CapitalUniversity.Sync.Host.Scheduling;

/// <summary>
/// Hangfire entry point for the recurring Treasury reconciliation sweep.
/// Resolved from DI per execution; delegates to <see cref="IReconciliationService"/>.
/// </summary>
public sealed class TreasuryReconciliationTrigger
{
    private readonly IReconciliationService _reconciliation;

    public TreasuryReconciliationTrigger(IReconciliationService reconciliation) => _reconciliation = reconciliation;

    public Task RunAsync(CancellationToken cancellationToken) => _reconciliation.RunAsync(cancellationToken);
}
