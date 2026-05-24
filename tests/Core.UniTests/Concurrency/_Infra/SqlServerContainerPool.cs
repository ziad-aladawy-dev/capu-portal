using Testcontainers.MsSql;

namespace CapitalUniversity.Core.UniTests.Concurrency._Infra;

/// <summary>
/// Process-singleton SQL Server container used by every per-test
/// <see cref="SqlServerDbFixture"/>. Starting a SQL Server container takes
/// 10-25 seconds — paying that cost once per test process (then handing out
/// per-test databases inside the running container) is the only sensible
/// economy when the concurrency suite has multiple tests.
///
/// <para>
/// <b>Lifecycle.</b> First caller to <see cref="GetAsync"/> wins the lock
/// and starts the container. Subsequent callers (whether from parallel test
/// fixtures or sequential ones) get the same already-started instance. On
/// process exit, Testcontainers' Ryuk sidecar reaps the container even if
/// no explicit dispose runs — which is what we want, since xUnit v2 has no
/// hook for assembly-level teardown.
/// </para>
///
/// <para>
/// <b>Failure caching.</b> If Docker is not running or the image pull
/// fails, the failure is captured once and returned to every caller —
/// repeated probes against a broken host would only add latency. Tests
/// then skip cleanly via <c>Skip.IfNot(_fixture.IsAvailable, …)</c>.
/// </para>
///
/// <para>
/// <b>Env-var override.</b> If <c>CAPU_TEST_SQL_CONNECTION</c> is set
/// (template containing a literal <c>{db}</c> placeholder), the pool is
/// bypassed entirely and the fixture talks to that pre-existing SQL Server
/// directly. Lets the dev / CI bypass Docker when an external instance is
/// preferred.
/// </para>
/// </summary>
internal static class SqlServerContainerPool
{
    public const string EnvVarConnectionTemplate = "CAPU_TEST_SQL_CONNECTION";

    // Strong SA password — the user spec requires it. Not a secret since the
    // container is ephemeral and not exposed beyond the test host.
    private const string SaPassword = "Capu_Test!_StrongP@ssw0rd_2026";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static MsSqlContainer? _container;
    private static bool _probed;
    private static string? _failReason;

    /// <summary>
    /// Result of the pool probe: either the live container's base connection
    /// string, or a human-readable reason the SQL backend is unavailable
    /// (Docker not running, image pull failed, env var malformed, etc.).
    /// </summary>
    public readonly record struct PoolResult(string? BaseConnectionString, string? FailReason);

    /// <summary>
    /// Returns either the shared container's connection string or a fail
    /// reason. Idempotent — safe to call from any thread, any time.
    /// </summary>
    public static async Task<PoolResult> GetAsync()
    {
        // Fast path — most calls happen after the first one has resolved.
        if (_probed)
        {
            return new PoolResult(_container?.GetConnectionString(), _failReason);
        }

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_probed)
            {
                return new PoolResult(_container?.GetConnectionString(), _failReason);
            }

            // Path 1 — env-var override. Tests bypass Docker entirely and
            // talk to whatever SQL Server the operator points at. The
            // fixture handles the {db} substitution per-test.
            var envTemplate = Environment.GetEnvironmentVariable(EnvVarConnectionTemplate);
            if (!string.IsNullOrWhiteSpace(envTemplate))
            {
                if (!envTemplate.Contains("{db}", StringComparison.Ordinal))
                {
                    _failReason =
                        $"'{EnvVarConnectionTemplate}' must contain the literal '{{db}}' placeholder so the fixture can substitute a per-test database name.";
                }
                else
                {
                    // Signal "use the env var directly" by returning a marker
                    // that the fixture recognises: BaseConnectionString is
                    // the template itself, with {db} still in it. The
                    // fixture will replace before opening.
                    _probed = true;
                    return new PoolResult(envTemplate, null);
                }
                _probed = true;
                return new PoolResult(null, _failReason);
            }

            // Path 2 — Docker. Start a fresh MS SQL Server 2022 container.
            // Testcontainers chooses an unused host port, exposes only that
            // port locally, and reaps the container on process exit via
            // Ryuk (no manual cleanup required).
            try
            {
                var built = new MsSqlBuilder()
                    // Pin to 2022-latest to match production. Locked image
                    // means no surprise refresh between CI runs.
                    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                    .WithPassword(SaPassword)
                    // Wait until the SQL Server reports ready — Testcontainers
                    // tries /opt/mssql-tools/bin/sqlcmd inside the container
                    // and only returns from StartAsync once an actual query
                    // succeeds. Saves us from racing the container's boot.
                    .Build();

                await built.StartAsync().ConfigureAwait(false);
                _container = built;
            }
            catch (Exception ex)
            {
                _failReason =
                    $"Docker-based SQL Server unavailable. Either run Docker / Docker Desktop, or set the {EnvVarConnectionTemplate} env var to a connection-string template (must contain literal '{{db}}'). Underlying: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
            }
            _probed = true;
            return new PoolResult(_container?.GetConnectionString(), _failReason);
        }
        finally
        {
            Gate.Release();
        }
    }
}
