using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Drains <see cref="IAuditLogQueue"/> and persists each <see cref="LogEntry"/> to
/// the configured Mongo collection. One reader per process (the channel is set up
/// <c>SingleReader = true</c>).
///
/// <para>
/// Failures bubble back to a per-batch try/catch: a transient Mongo outage logs an
/// internal warning via <see cref="ILogger"/> (Serilog → console) so the operator
/// notices, but does NOT stop the worker. The queue keeps accepting writes; entries
/// just back up until Mongo is reachable. If the queue fills, new entries are
/// dropped (BoundedChannel DropWrite policy) and that loss is observable via
/// <see cref="IAuditLogQueue.Count"/>.
/// </para>
/// </summary>
public class AuditLogFlushWorker : BackgroundService
{
    private readonly IAuditLogQueue _queue;
    private readonly IMongoClient _mongoClient;
    private readonly MongoSettings _mongoSettings;
    private readonly ILogger<AuditLogFlushWorker> _logger;

    public AuditLogFlushWorker(
        IAuditLogQueue queue,
        IMongoClient mongoClient,
        IOptions<MongoSettings> mongoSettings,
        ILogger<AuditLogFlushWorker> logger)
    {
        _queue = queue;
        _mongoClient = mongoClient;
        _mongoSettings = mongoSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IMongoCollection<LogEntry>? collection = null;
        try
        {
            var db = _mongoClient.GetDatabase(_mongoSettings.DatabaseName);
            collection = db.GetCollection<LogEntry>(_mongoSettings.LogsCollection);
        }
        catch (Exception ex)
        {
            // Mongo unreachable at startup → keep draining (and discarding) so the
            // queue doesn't grow unbounded; surface the failure once.
            _logger.LogWarning(ex, "AuditLogFlushWorker: failed to resolve Mongo collection; entries will be drained and discarded until reachable.");
        }

        await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;
            if (collection is null) continue;

            try
            {
                await collection.InsertOneAsync(entry, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Per-entry catch so one bad write doesn't poison the loop. The entry
                // itself is lost — we don't retry because the queue gives at-most-once
                // semantics; losing a log row is preferable to wedging the worker.
                _logger.LogWarning(ex, "AuditLogFlushWorker: insert failed for log id {Id}; entry dropped.", entry.Id);
            }
        }
    }
}
