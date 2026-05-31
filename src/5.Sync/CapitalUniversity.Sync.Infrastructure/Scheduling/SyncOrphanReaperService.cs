using System.Diagnostics;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Scheduling;

/// <summary>
/// Phase X hardening fix #5. Scans for orphan runs and marks them Failed so
/// operators see them in the dashboard instead of having them sit invisibly in
/// <c>Enqueued</c> forever.
///
/// <para>
/// An orphan is a <c>sync.runs</c> row in status <c>Enqueued</c> with
/// <c>HangfireJobId IS NULL</c> that is older than
/// <see cref="SyncOrphanReaperOptions.GraceMinutes"/>. The dispatcher opens
/// the row first, then enqueues the Hangfire job, then links the job id — so
/// a row in this state after the grace window means the enqueue step failed
/// (e.g. Hangfire SQL outage) AND the dispatcher's exception path also failed
/// to mark the row as Failed (which itself is documented as a Phase 7 Hardening
/// concern). The reaper closes that final gap.
/// </para>
///
/// <para>
/// Goes through the existing <see cref="ISyncRunRepository.FindOrphanRunsAsync"/>
/// and <see cref="ISyncRunRepository.MarkFailedAsync"/> — no new audit code
/// paths, no schema change.
/// </para>
/// </summary>
public sealed class SyncOrphanReaperService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SyncOrphanReaperOptions> _options;
    private readonly ISyncLogger _logger;

    public SyncOrphanReaperService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SyncOrphanReaperOptions> options,
        ISyncLogger logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var correlationId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();

        if (!opts.Enabled)
        {
            _logger.LogInformation(correlationId,
                "Orphan reaper skipped — Sync:OrphanReaper:Enabled = false.");
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-opts.GraceMinutes);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISyncRunRepository>();

        var orphans = await repo.FindOrphanRunsAsync(cancellationToken).ConfigureAwait(false);
        var eligible = orphans.Where(o => o.EnqueuedAt < cutoff).Take(opts.MaxReapedPerRun).ToList();

        if (eligible.Count == 0)
        {
            _logger.LogInformation(correlationId,
                "Orphan reaper: no eligible orphans. TotalOrphansFound={Total} GraceMinutes={Grace} ElapsedMs={Ms}",
                orphans.Count, opts.GraceMinutes, sw.ElapsedMilliseconds);
            return;
        }

        var reaped = 0;
        foreach (var orphan in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await repo.MarkFailedAsync(
                    orphan.CorrelationId,
                    $"Orphaned: sync.runs row stayed Enqueued without a Hangfire job for more than {opts.GraceMinutes} minutes. Likely a dispatcher-enqueue failure where the audit row was created but the Hangfire enqueue did not complete.",
                    cancellationToken).ConfigureAwait(false);

                reaped++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(correlationId,
                    "Orphan reaper failed to mark run as Failed. CorrelationId={CorrelationId} Module={Module} Error={Error}",
                    orphan.CorrelationId, orphan.ModuleName, ex.Message);
            }
        }

        sw.Stop();
        _logger.LogWarning(correlationId,
            "Orphan reaper completed. Reaped={Reaped}/{Eligible} TotalOrphansFound={Total} GraceMinutes={Grace} ElapsedMs={Ms}",
            reaped, eligible.Count, orphans.Count, opts.GraceMinutes, sw.ElapsedMilliseconds);
    }
}