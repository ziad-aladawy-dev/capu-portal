using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CapitalUniversity.Sync.Infrastructure.Observability;

/// <summary>
/// Generic SQL Server connectivity probe used by Phase 10 health checks for both the
/// Hangfire storage DB and operator-declared module DBs. Opens a connection, runs
/// <c>SELECT 1</c>, and returns Healthy/Unhealthy.
///
/// <para>
/// Designed to be cheap (~1 round trip) so it can run on every <c>/healthz/ready</c>
/// poll without contention. The probe does NOT validate schema presence — that's
/// the Phase 2/3/5/7 startup migrations' responsibility.
/// </para>
/// </summary>
public sealed class SqlConnectivityHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly string _description;

    public SqlConnectivityHealthCheck(string connectionString, string description)
    {
        _connectionString = connectionString;
        _description = description;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy($"{_description}: connection string empty.");
        }

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 5;
            _ = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy($"{_description}: connected.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_description}: {ex.Message}", ex);
        }
    }
}