using CapitalUniversity.Sync.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Observability;

/// <summary>
/// Read-only Phase 10 probe over Hangfire's SQL storage. Returns per-queue depth +
/// oldest-pending age. Used by the <c>GET /admin/queues/lag</c> endpoint and by
/// future alerting flows. Pure read — never mutates Hangfire state.
///
/// <para>
/// Implementation uses a single query joining <c>[HangFire].[JobQueue]</c> (the
/// queued-but-not-fetched rows) against <c>[HangFire].[Job]</c> for the enqueue
/// timestamp. <c>[HangFire].[State]</c> resolves currently-processing rows.
/// </para>
/// </summary>
public sealed class QueueLagProbe
{
    private readonly IOptionsMonitor<SyncHangfireOptions> _hangfireOptions;

    public QueueLagProbe(IOptionsMonitor<SyncHangfireOptions> hangfireOptions)
    {
        _hangfireOptions = hangfireOptions;
    }

    public async Task<IReadOnlyList<QueueLagSnapshot>> SampleAsync(CancellationToken cancellationToken)
    {
        var opts = _hangfireOptions.CurrentValue;
        var connectionString = opts.ConnectionString;
        var schema = opts.SchemaName;
        var listenList = opts.Queues ?? new List<string> { "default" };

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Array.Empty<QueueLagSnapshot>();
        }

        var snapshots = new List<QueueLagSnapshot>(listenList.Count);

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var queue in listenList.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Enqueued depth + oldest CreatedAt (Job.CreatedAt is closest proxy to
            // "enqueued time" available without a join through the state log).
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 5;
            cmd.CommandText = $@"
SELECT
    COUNT(jq.Id) AS EnqueuedCount,
    MIN(j.CreatedAt) AS OldestCreatedAt
FROM [{EscapeIdentifier(schema)}].[JobQueue] jq
LEFT JOIN [{EscapeIdentifier(schema)}].[Job] j ON j.Id = jq.JobId
WHERE jq.Queue = @queue AND jq.FetchedAt IS NULL;

SELECT COUNT(jq.Id) AS ProcessingCount
FROM [{EscapeIdentifier(schema)}].[JobQueue] jq
WHERE jq.Queue = @queue AND jq.FetchedAt IS NOT NULL;
";
            cmd.Parameters.Add(new SqlParameter("@queue", queue));

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            int enqueuedCount = 0;
            DateTimeOffset? oldestCreated = null;
            int processingCount = 0;

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                enqueuedCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                if (!reader.IsDBNull(1))
                {
                    var raw = reader.GetDateTime(1);
                    oldestCreated = new DateTimeOffset(DateTime.SpecifyKind(raw, DateTimeKind.Utc));
                }
            }

            if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false) &&
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                processingCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            }

            snapshots.Add(new QueueLagSnapshot
            {
                Queue = queue,
                EnqueuedCount = enqueuedCount,
                ProcessingCount = processingCount,
                OldestEnqueuedAt = oldestCreated,
                OldestAge = oldestCreated.HasValue
                    ? DateTimeOffset.UtcNow - oldestCreated.Value
                    : null
            });
        }

        return snapshots;
    }

    private static string EscapeIdentifier(string raw) => raw.Replace("]", "]]");
}