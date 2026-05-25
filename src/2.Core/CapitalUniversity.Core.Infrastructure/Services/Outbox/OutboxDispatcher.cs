using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

/// <summary>
/// Background drain loop for the outbox table. Each tick:
///   1. Loads a batch of pending rows (ProcessedAt IS NULL, AttemptCount &lt; max), oldest first.
///   2. Dispatches each row to the matching <see cref="IOutboxMessageHandler"/>.
///   3. On handler success → stamp <c>ProcessedAt</c>; on failure → bump
///      <c>AttemptCount</c> + record <c>LastError</c>. Either way, save.
/// </summary>
public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly OutboxOptions _options;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown — exit cleanly.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxDispatcher batch failed; will retry on next tick.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var executionContext = scope.ServiceProvider.GetRequiredService<IExecutionContext>();

        // Runtime Hardening Plan §3.2 + §3.4 — outbox runs without an HttpContext.
        // 1. Activate SystemExecutionScope so authorization guards (EffectiveScope)
        //    permit the trusted background operations.
        // 2. Stamp the system actor on every log line emitted from inside the batch
        //    so audit / correlation searches do not see anonymous mutations.
        using var systemScope = new SystemExecutionScope(executionContext);
        using var actorScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Actor"] = SystemActors.OutboxDispatcher,
            ["ActorKind"] = SystemActors.DisplayName,
        });

        var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .GroupBy(h => h.MessageType)
            .ToDictionary(g => g.Key, g => g.First());

        // M12 — Single SELECT, ordered by EnqueuedAt across ALL message types,
        // capped at BatchSize. The foreach below iterates this list in order,
        // so dispatch is globally oldest-first regardless of handler grouping.
        // Do NOT reorder by message type here — the contract with producers is
        // "rows enqueued earlier are processed first", which is the only
        // ordering guarantee they can rely on when staging a series of related
        // events in the same transaction.
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.AttemptCount < _options.MaxAttempts)
            .OrderBy(m => m.EnqueuedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0) return;

        foreach (var row in batch)
        {
            row.AttemptCount += 1;

            if (!handlers.TryGetValue(row.MessageType, out var handler))
            {
                row.LastError = $"No handler registered for message type '{row.MessageType}'.";
                if (!row.IsPoisoned && row.AttemptCount >= _options.MaxAttempts)
                {
                    row.IsPoisoned = true;
                    row.PoisonedAt = DateTime.UtcNow;
                    _logger.LogError(
                        "Outbox message {MessageId} of type '{Type}' poisoned: no handler registered.",
                        row.Id, row.MessageType);
                }
                continue;
            }

            // Restore the originating request's context for the duration of this
            // specific message's execution.
            using var cultureScope = !string.IsNullOrEmpty(row.Culture)
                ? new SystemCultureScope(row.Culture)
                : null;

            using var correlationScope = !string.IsNullOrEmpty(row.CorrelationId)
                ? _logger.BeginScope(new Dictionary<string, object> { [CorrelationContext.ItemKey] = row.CorrelationId })
                : null;

            try
            {
                await handler.HandleAsync(row.Id, row.Payload, cancellationToken);
                row.ProcessedAt = DateTime.UtcNow;
                row.LastError = null;
            }
            catch (Exception ex)
            {
                row.LastError = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogWarning(ex, "Outbox handler '{Type}' failed (attempt {Attempt}).", row.MessageType, row.AttemptCount);

                // First time we hit the retry cap, mark the row poisoned so it's
                // visible to operators (and to OutboxService.GetPoisonedAsync).
                // The row stays in the table — never auto-purged — so on-call can
                // inspect the payload + LastError before requeuing or dropping.
                if (!row.IsPoisoned && row.AttemptCount >= _options.MaxAttempts)
                {
                    row.IsPoisoned = true;
                    row.PoisonedAt = DateTime.UtcNow;
                    _logger.LogError(
                        "Outbox message {MessageId} of type '{Type}' poisoned after {Attempts} attempts. LastError: {LastError}",
                        row.Id, row.MessageType, row.AttemptCount, row.LastError);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
