using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CapitalUniversity.API.Infrastructure;

/// <summary>
/// Generic SQL Server connectivity probe used for API readiness checks.
/// Opens a connection, runs <c>SELECT 1</c>, and returns Healthy/Unhealthy.
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