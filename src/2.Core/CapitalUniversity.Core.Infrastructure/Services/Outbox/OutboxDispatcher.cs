using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
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
        var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .GroupBy(h => h.MessageType)
            .ToDictionary(g => g.Key, g => g.First());

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
                continue;
            }

            try
            {
                await handler.HandleAsync(row.Payload, cancellationToken);
                row.ProcessedAt = DateTime.UtcNow;
                row.LastError = null;
            }
            catch (Exception ex)
            {
                row.LastError = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogWarning(ex, "Outbox handler '{Type}' failed (attempt {Attempt}).", row.MessageType, row.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
