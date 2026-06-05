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
        // Resolved lazily and re-attempted while null so the worker self-heals: if
        // Mongo is unreachable at startup, resolution simply fails (logged once via
        // _resolveFailureLogged) and we retry on the next drained entry. When Mongo
        // recovers, the next entry resolves the collection and persistence resumes
        // — no restart required.
        IMongoCollection<LogEntry>? collection = TryResolveCollection();

        await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;

            collection ??= TryResolveCollection();
            if (collection is null) continue;

            // Ensure the read-side indexes exist (idempotent). Done lazily off the
            // first reachable collection so a Mongo outage at startup doesn't block
            // the worker — when Mongo recovers and the first entry resolves the
            // collection, the indexes are created then.
            if (!_indexesEnsured)
            {
                await EnsureIndexesAsync(collection, stoppingToken);
            }

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
                // Per-entry catch so one bad write doesn't poison the loop. Drop the
                // handle so the next iteration re-resolves — a transient outage that
                // invalidated the connection won't strand the worker on a dead handle.
                collection = null;
                _logger.LogWarning(ex, "AuditLogFlushWorker: insert failed for log id {Id}; entry dropped.", entry.Id);
            }
        }
    }

    private bool _resolveFailureLogged;
    private bool _indexesEnsured;

    /// <summary>
    /// Creates the indexes backing the audit read API (filter + sort). Idempotent:
    /// Mongo's createIndexes is a no-op when an identical index already exists, so
    /// this is safe to call on every startup and across both hosts (API + Sync).
    /// A failure is logged and left for the next entry to retry — it never stops
    /// the worker.
    /// </summary>
    private async Task EnsureIndexesAsync(IMongoCollection<LogEntry> collection, CancellationToken cancellationToken)
    {
        try
        {
            var keys = Builders<LogEntry>.IndexKeys;
            var models = new[]
            {
                // Primary sort + a compound (Category, time) for the common
                // "filter by type, newest first" query shape.
                new CreateIndexModel<LogEntry>(keys.Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions { Name = "ix_createdAtUtc_desc" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.Category).Descending(x => x.CreatedAtUtc),
                    new CreateIndexOptions { Name = "ix_category_createdAtUtc" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.Level),
                    new CreateIndexOptions { Name = "ix_level" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.Action),
                    new CreateIndexOptions { Name = "ix_action" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.EntityName),
                    new CreateIndexOptions { Name = "ix_entityName" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.Role),
                    new CreateIndexOptions { Name = "ix_role" }),
                new CreateIndexModel<LogEntry>(keys.Ascending(x => x.UserName),
                    new CreateIndexOptions { Name = "ix_userName" }),
            };

            await collection.Indexes.CreateManyAsync(models, cancellationToken);
            _indexesEnsured = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuditLogFlushWorker: failed to ensure audit-log indexes; will retry.");
        }
    }

    private IMongoCollection<LogEntry>? TryResolveCollection()
    {
        try
        {
            var db = _mongoClient.GetDatabase(_mongoSettings.DatabaseName);
            var collection = db.GetCollection<LogEntry>(_mongoSettings.LogsCollection);
            _resolveFailureLogged = false; // reset so a later outage logs again
            return collection;
        }
        catch (Exception ex)
        {
            // Surface the failure once per outage (not once per dropped entry) to
            // avoid flooding the log while Mongo is down.
            if (!_resolveFailureLogged)
            {
                _resolveFailureLogged = true;
                _logger.LogWarning(ex, "AuditLogFlushWorker: failed to resolve Mongo collection; entries dropped until reachable.");
            }
            return null;
        }
    }
}
