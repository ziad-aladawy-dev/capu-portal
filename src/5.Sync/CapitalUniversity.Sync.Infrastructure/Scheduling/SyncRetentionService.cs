using System.Diagnostics;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Scheduling;

/// <summary>
/// Phase 9 audit + outbox retention. Single Hangfire recurring job invokes
/// <see cref="RunAsync"/>; the service issues batched <c>DELETE TOP (N)</c> loops
/// against each target table.
///
/// <para>
/// <b>Crash-safe.</b> Each <c>DELETE TOP (N)</c> commits independently — a host
/// restart resumes from wherever the previous run left off (the WHERE clause is
/// the cursor). The hard <see cref="SyncRetentionOptions.MaxDeletedPerTablePerRun"/>
/// cap bounds wall-clock per run.
/// </para>
///
/// <para>
/// <b>Schema-respectful.</b> Hangfire's <c>[HangFire]</c> schema is never touched
/// (it has its own state-bound expiration). <see cref="SyncCheckpointStore"/>'s
/// <c>sync.checkpoints</c> is operational state, never deleted. The deletion order
/// is children-before-parents (<c>sync.failures</c>, <c>sync.dead_letters</c>,
/// <c>sync.jobs</c>, then <c>sync.runs</c>) so a partial run leaves the audit
/// queryable.
/// </para>
/// </summary>
public sealed class SyncRetentionService
{
    private readonly IOptionsMonitor<SyncRetentionOptions> _options;
    private readonly IOptionsMonitor<SyncHangfireOptions> _hangfireOptions;
    private readonly ISyncLogger _logger;

    public SyncRetentionService(
        IOptionsMonitor<SyncRetentionOptions> options,
        IOptionsMonitor<SyncHangfireOptions> hangfireOptions,
        ISyncLogger logger)
    {
        _options = options;
        _hangfireOptions = hangfireOptions;
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
                "Sync retention skipped — Sync:Retention:Enabled = false. " +
                "Cron is registered but cleanup is opt-in.");
            return;
        }

        var connectionString = _hangfireOptions.CurrentValue.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning(correlationId,
                "Sync retention aborted — Hangfire connection string is empty.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var summary = new List<(string Target, int Deleted, long Ms)>();

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Children before parents: failures → dead_letters → jobs → runs.
            // sync.checkpoints intentionally untouched.

            // 1. sync.failures keyed by OccurredAt (actual column name in schema).
            summary.Add(await DeleteAuditAsync(
                conn,
                label: "sync.failures",
                table: "sync.failures",
                whereClause: "OccurredAt < @cutoff",
                cutoff: now.AddDays(-opts.FailureRowsRetentionDays),
                opts, correlationId, cancellationToken).ConfigureAwait(false));

            // 2. sync.dead_letters keyed by TerminalAt (actual column name in schema).
            summary.Add(await DeleteAuditAsync(
                conn,
                label: "sync.dead_letters",
                table: "sync.dead_letters",
                whereClause: "TerminalAt < @cutoff",
                cutoff: now.AddDays(-opts.DeadLettersRetentionDays),
                opts, correlationId, cancellationToken).ConfigureAwait(false));

            // 3. sync.jobs — tie to EnqueuedAt as a proxy for the parent run's age.
            summary.Add(await DeleteAuditAsync(
                conn,
                label: "sync.jobs",
                table: "sync.jobs",
                whereClause: "EnqueuedAt < @cutoff",
                cutoff: now.AddDays(-opts.SuccessfulRunsRetentionDays),
                opts, correlationId, cancellationToken).ConfigureAwait(false));

            // 4a. sync.runs (Succeeded = 2) — short window.
            summary.Add(await DeleteAuditAsync(
                conn,
                label: "sync.runs (Succeeded)",
                table: "sync.runs",
                whereClause: "Status = 2 AND CompletedAt < @cutoff",
                cutoff: now.AddDays(-opts.SuccessfulRunsRetentionDays),
                opts, correlationId, cancellationToken).ConfigureAwait(false));

            // 4b. sync.runs (Failed=3, DeadLettered=4, Cancelled=5) — long window.
            summary.Add(await DeleteAuditAsync(
                conn,
                label: "sync.runs (Failed/DeadLettered/Cancelled)",
                table: "sync.runs",
                whereClause: "Status IN (3, 4, 5) AND CompletedAt < @cutoff",
                cutoff: now.AddDays(-opts.FailedRunsRetentionDays),
                opts, correlationId, cancellationToken).ConfigureAwait(false));

            // 5. Operator-declared outbox tables. Generic — Sync.Infrastructure
            //    doesn't know which modules exist.
            foreach (var target in opts.OutboxTables ?? new List<SyncOutboxRetentionTarget>())
            {
                if (string.IsNullOrWhiteSpace(target.Schema) || string.IsNullOrWhiteSpace(target.Table))
                {
                    _logger.LogWarning(correlationId,
                        "Sync retention skipping malformed outbox target: Schema='{Schema}' Table='{Table}'.",
                        target.Schema, target.Table);
                    continue;
                }

                var qualifiedName = $"[{EscapeIdentifier(target.Schema)}].[{EscapeIdentifier(target.Table)}]";
                var statusCol = EscapeIdentifier(target.StatusColumn);
                var tsCol = EscapeIdentifier(target.TimestampColumn);

                summary.Add(await DeleteRawAsync(
                    conn,
                    target: $"{target.Schema}.{target.Table} (Processed)",
                    sqlTemplate: $"DELETE TOP ({{0}}) FROM {qualifiedName} WHERE [{statusCol}] = @status AND [{tsCol}] < @cutoff",
                    parameters: cmd =>
                    {
                        cmd.Parameters.Add(new SqlParameter("@status", target.ProcessedStatusValue));
                        cmd.Parameters.Add(new SqlParameter("@cutoff", now.AddDays(-target.RetentionDays)));
                    },
                    opts, correlationId, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(correlationId, ex,
                "Sync retention failed. ElapsedMs={ElapsedMs} Partial={Partial}",
                sw.ElapsedMilliseconds, summary.Count);
            throw;
        }

        sw.Stop();
        var totalDeleted = summary.Sum(s => s.Deleted);
        _logger.LogInformation(correlationId,
            "Sync retention completed. TotalDeleted={Total} TablesProcessed={Tables} ElapsedMs={ElapsedMs} Details={Details}",
            totalDeleted,
            summary.Count,
            sw.ElapsedMilliseconds,
            string.Join("; ", summary.Select(s => $"{s.Target}={s.Deleted}rows/{s.Ms}ms")));
    }

    private async Task<(string Target, int Deleted, long Ms)> DeleteAuditAsync(
        SqlConnection conn,
        string label,
        string table,
        string whereClause,
        DateTimeOffset cutoff,
        SyncRetentionOptions opts,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        // table is a hardcoded sync.* string from THIS file, never user input —
        // safe to interpolate directly. whereClause is also internal.
        return await DeleteRawAsync(
            conn,
            label,
            sqlTemplate: $"DELETE TOP ({{0}}) FROM {table} WHERE {whereClause}",
            parameters: cmd => cmd.Parameters.Add(new SqlParameter("@cutoff", cutoff)),
            opts,
            correlationId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Target, int Deleted, long Ms)> DeleteRawAsync(
        SqlConnection conn,
        string target,
        string sqlTemplate,
        Action<SqlCommand> parameters,
        SyncRetentionOptions opts,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var totalDeleted = 0;
        var sql = string.Format(sqlTemplate, opts.DeleteBatchSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (opts.MaxDeletedPerTablePerRun > 0 && totalDeleted >= opts.MaxDeletedPerTablePerRun)
            {
                _logger.LogWarning(correlationId,
                    "Sync retention hit MaxDeletedPerTablePerRun cap. Target={Target} Deleted={Deleted} Cap={Cap}. Remaining backlog will drain on the next tick.",
                    target, totalDeleted, opts.MaxDeletedPerTablePerRun);
                break;
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            parameters(cmd);

            int rows;
            try
            {
                rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(correlationId,
                    "Sync retention SQL error on {Target}: {Error}. Skipping remaining batches for this target.",
                    target, ex.Message);
                break;
            }

            if (rows == 0) break;
            totalDeleted += rows;
        }

        sw.Stop();
        return (target, totalDeleted, sw.ElapsedMilliseconds);
    }

    private static string EscapeIdentifier(string raw) =>
        // SQL Server bracket-quoting: a literal ']' becomes ']]'.
        raw.Replace("]", "]]");
}